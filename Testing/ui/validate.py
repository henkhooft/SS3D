"""One-command validation for UI/session work. Use Testing/ui/validate.sh or compile.sh.

Steps: preflight → compile → editmode → visual → lifecycle → build (if stale) → remote.
--quick stops after lifecycle. Every step has an expected duration and a timeout: past the
expected duration the dialog watchdog runs every 30 s (a modal dialog fails the step at once);
on timeout the last editor.log lines are printed. Exits nonzero on any failure and writes
artifacts/ui/validate-<UTC>/summary.md.
"""
import argparse
import asyncio
import json
import re
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import NamedTuple

from fastmcp import Client

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "artifacts/ui"
EDITOR_LOG = UI / "editor.log"
COMPILE_RESULT = UI / "compile-result.json"
BUILD_RESULT = UI / "client-build-result.json"
LOG_ERROR = re.compile(r"(: error [A-Za-z]+\d+:|error CS\d+)")
ORDER = ["preflight", "compile", "editmode", "visual", "lifecycle", "build", "remote"]
QUICK = ORDER[:5]
PREFLIGHT = 'return string.Join("\\n", SS3D.Editor.Automation.AutomationPreflight.Check());'


class Done(NamedTuple):
    note: str
    report: str = ""
    pngs: tuple = ()


class StepFailed(Exception):
    def __init__(self, message, report=None):
        super().__init__(message)
        self.report = report


class Editor:
    def __init__(self, client):
        self.client = client

    async def connect(self):
        data = json.loads((await self.client.read_resource("mcpforunity://instances"))[0].text)
        matches = [i for i in data.get("instances", []) if i.get("name") == ROOT.name]
        if len(matches) != 1:
            raise StepFailed(f"Expected one SS3D editor at {ROOT}; instances: {data.get('instances')}")
        await self.client.call_tool("set_active_instance", {"instance": matches[0]["id"]})
        info = json.loads((await self.client.read_resource("mcpforunity://project/info"))[0].text)
        if Path(info["data"]["projectRoot"]).resolve() != ROOT:
            raise StepFailed("Connected editor belongs to a different checkout")

    async def execute(self, code):
        result = await self.client.call_tool("execute_code", {"action": "execute", "code": code}, raise_on_error=False)
        payload = json.loads(result.content[0].text)
        if not payload.get("success"):
            raise StepFailed(f"execute_code failed: {payload.get('message') or payload.get('error')}\n{json.dumps(payload.get('data'))[:2000]}")
        return (payload.get("data") or {}).get("result")

    async def menu(self, path):
        result = await self.client.call_tool("execute_menu_item", {"menu_path": path}, raise_on_error=False)
        if result.is_error:
            raise StepFailed(f"{path}: {result.content[0].text if result.content else result}")

    async def busy(self):
        """Why the editor can't take the next step, or None. Asks the editor itself: the state
        resource's ready_for_tools reports a 'stale_status' heartbeat while an idle editor is fine."""
        try:
            return await self.execute(
                'if (EditorApplication.isPlayingOrWillChangePlaymode) return "in Play Mode";'
                'if (EditorApplication.isCompiling) return "compiling";'
                'if (EditorApplication.isUpdating) return "importing assets";'
                'return "";') or None
        except Exception as error:
            return f"bridge unavailable ({str(error).splitlines()[0][:120]})"

    async def wait_ready(self, timeout=180):
        """Out of Play Mode, not compiling or importing, bridge answering."""
        deadline = time.monotonic() + timeout
        reason = "?"
        while time.monotonic() < deadline:
            reason = await self.busy()
            if reason is None:
                return
            await asyncio.sleep(1)
        raise asyncio.TimeoutError(f"editor not ready after {timeout}s: {reason}")


def rel(path):
    return str(Path(path).relative_to(ROOT))


def log_lines():
    if not EDITOR_LOG.exists():
        return []
    return EDITOR_LOG.read_text(errors="replace").splitlines()


def watchdog():
    """(dialog found, output)."""
    proc = subprocess.run([str(ROOT / "Testing/ui/watch-dialogs.sh")], capture_output=True, text=True)
    return proc.returncode == 1, (proc.stdout + proc.stderr).strip()


async def wait_for(check, expected, timeout, what):
    """Poll check() each second; watchdog after `expected` seconds, fail on a dialog or timeout."""
    start = time.monotonic()
    next_watch = start + expected
    while True:
        value = await check()
        if value is not None:
            return value
        now = time.monotonic()
        if now - start > timeout:
            found, output = watchdog()
            tail = [line for line in log_lines() if line.strip()][-40:]
            raise StepFailed(f"Timed out after {timeout}s waiting for {what}.\nWatchdog: {output or 'no dialogs'}\n"
                             "Last editor.log lines:\n  " + "\n  ".join(tail))
        if now >= next_watch:
            next_watch = now + 30
            found, output = watchdog()
            if found:
                raise StepFailed(f"Modal dialog while waiting for {what}:\n{output}")
        await asyncio.sleep(1)


def newest_report(pattern, before):
    created = set(UI.glob(pattern)) - before
    if not created:
        return None
    path = max(created, key=lambda p: p.stat().st_mtime)
    try:
        report = json.loads(path.read_text())
    except json.JSONDecodeError:
        return None
    return (path, report) if report.get("status") in ("passed", "failed") else None


# ---- steps -----------------------------------------------------------------------------------


async def step_preflight(editor, ctx):
    await editor.wait_ready()
    problems = await editor.execute(PREFLIGHT)
    if problems:
        raise StepFailed("Preflight problems (not saved or discarded; the owner decides):\n- "
                         + problems.replace("\n", "\n- "))
    return Done("open scenes clean")


async def step_compile(editor, ctx):
    await editor.wait_ready()
    marker = len(log_lines())
    requested = int(time.time() * 1000)
    await editor.execute("AssetDatabase.Refresh(); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); return \"ok\";")

    async def finished():
        result = json.loads(COMPILE_RESULT.read_text()) if COMPILE_RESULT.exists() else {}
        if result.get("finishedUnixMs", 0) < requested:
            return None
        return None if await editor.busy() else result

    result = await wait_for(finished, 90, 600, "script compilation")
    errors = list(result.get("errors", []))
    seen = set(errors)
    for line in log_lines()[marker:]:
        if LOG_ERROR.search(line) and line.strip() not in seen:
            errors.append(line.strip())
            seen.add(line.strip())
    failed = await editor.execute("return EditorUtility.scriptCompilationFailed.ToString();")
    seconds = (result["finishedUnixMs"] - result["startedUnixMs"]) / 1000
    if errors or failed == "True":
        assemblies = ", ".join(result.get("failedAssemblies", [])) or "?"
        raise StepFailed(f"Compilation failed ({assemblies}; scriptCompilationFailed={failed}):\n  "
                         + "\n  ".join(errors or ["(no messages captured; see editor.log)"]), rel(COMPILE_RESULT))
    return Done(f"no errors ({seconds:.1f}s compile)", rel(COMPILE_RESULT))


async def step_editmode(editor, ctx):
    await editor.wait_ready()
    started = await editor.client.call_tool("run_tests", {"mode": "EditMode"}, raise_on_error=False)
    payload = json.loads(started.content[0].text)
    job = (payload.get("data") or {}).get("job_id")
    if not job:
        raise StepFailed(f"run_tests did not start: {json.dumps(payload)[:1000]}")

    async def done():
        try:
            polled = await editor.client.call_tool(
                "get_test_job", {"job_id": job, "wait_timeout": 20, "include_failed_tests": True}, raise_on_error=False)
            data = json.loads(polled.content[0].text).get("data") or {}
        except Exception:
            return None  # domain reload between polls
        return data if data.get("status") in ("succeeded", "failed", "completed", "error") else None

    data = await wait_for(done, 300, 1200, "EditMode tests")
    # The Test Runner restores the scene setup after the job reports done; tests that open scenes
    # leave them dirty until then. Wait for the restore, then hold the tests to the same preflight.
    deadline = time.monotonic() + 60
    while True:
        try:
            problems = await editor.execute(PREFLIGHT)
        except StepFailed as error:  # the bridge refuses calls mid-restore
            problems = f"editor busy restoring scenes ({str(error).splitlines()[0]})"
        if not problems or time.monotonic() > deadline:
            break
        await asyncio.sleep(1)
    report = ctx["out"] / "editmode.json"
    report.write_text(json.dumps(data, indent=2))
    summary = (data.get("result") or {}).get("summary") or {}
    failed = summary.get("failed", 0)
    if data.get("status") != "succeeded" or failed:
        names = [t.get("fullName") or t.get("name") for t in (data.get("result") or {}).get("results", [])
                 if (t.get("state") or t.get("result") or "").lower().startswith("fail")]
        raise StepFailed(f"EditMode: {failed} failed of {summary.get('total', '?')}; status {data.get('status')} {data.get('error') or ''}\n  "
                         + "\n  ".join(names[:30]), rel(report))
    if problems:
        raise StepFailed("EditMode tests left the editor polluted:\n- " + problems.replace("\n", "\n- "), rel(report))
    return Done(f"{summary.get('passed', '?')}/{summary.get('total', '?')} passed", rel(report))


async def run_pilot(editor, menu, pattern, expected, timeout):
    await editor.wait_ready()
    before = set(UI.glob(pattern))
    await editor.menu(menu)

    async def report():
        return newest_report(pattern, before)

    path, data = await wait_for(report, expected, timeout, menu)
    report_path = rel(path)
    if data["status"] != "passed":
        detail = data.get("failure") or ""
        if data.get("errors"):
            detail += "\nCaptured errors:\n  " + "\n  ".join(e.splitlines()[0] for e in data["errors"][:10])
        raise StepFailed(f"{menu} failed: {detail}", report_path)
    return Done(f"{len(data.get('checks') or [])} checks", report_path, tuple(sorted(rel(p) for p in path.parent.glob("*.png"))))


async def step_visual(editor, ctx):
    return await run_pilot(editor, "SS3D/Testing/Run Lobby UI Pilot", "[0-9]*/status.json", 120, 300)


async def step_lifecycle(editor, ctx):
    return await run_pilot(editor, "SS3D/Testing/Run Lobby Lifecycle Pilot", "lifecycle-*/status.json", 150, 420)


async def step_build(editor, ctx):
    await editor.wait_ready()
    stale = await editor.execute('return SS3D.Editor.Automation.AutomationBuild.ClientStaleReason() ?? "";')
    if not stale:
        return Done("client build current (skipped)")
    print(f"  {stale}; rebuilding (warm ~25 s, cold ~13 min; the editor is blocked meanwhile)", flush=True)
    requested = datetime.now(timezone.utc)
    await editor.execute("SS3D.Editor.Automation.AutomationBuild.BuildClient(); return \"scheduled\";")

    async def finished():
        try:
            result = json.loads(BUILD_RESULT.read_text())
        except (OSError, json.JSONDecodeError):
            return None
        started = datetime.fromisoformat(result.get("startedUtc", "1970-01-01T00:00:00+00:00").replace("Z", "+00:00"))
        if started.tzinfo is None:
            started = started.replace(tzinfo=timezone.utc)
        if started < requested.replace(microsecond=0) or result.get("status") == "building":
            return None
        return result

    result = await wait_for(finished, 120, 1800, "client build")
    if result["status"] != "succeeded":
        raise StepFailed(f"Client build failed: {result.get('error')}", rel(BUILD_RESULT))
    return Done(f"rebuilt {result['sourceHash']} in {result['seconds']:.0f}s", rel(BUILD_RESULT))


async def step_remote(editor, ctx):
    return await run_pilot(editor, "SS3D/Testing/Run Lobby Remote Client Pilot", "remote-*/status.json", 120, 420)


STEPS = {name: globals()["step_" + name] for name in ORDER}


async def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--quick", action="store_true", help="preflight → lifecycle (default)")
    mode.add_argument("--full", action="store_true", help="all steps, including client build and remote pilot")
    mode.add_argument("--steps", help="comma-separated subset, e.g. compile or visual,remote")
    args = parser.parse_args()
    steps = args.steps.split(",") if args.steps else ORDER if args.full else QUICK
    unknown = [s for s in steps if s not in STEPS]
    if unknown:
        parser.error(f"unknown steps {unknown}; choose from {ORDER}")

    out = UI / ("validate-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S"))
    out.mkdir(parents=True, exist_ok=True)
    ctx = {"out": out}
    rows, pngs, failures = [], [], []
    async def quiet(_message):
        pass  # the server's INFO notifications would bury the step output

    async with Client("http://127.0.0.1:8080/mcp", timeout=120, log_handler=quiet) as client:
        editor = Editor(client)
        await editor.connect()
        for name in steps:
            print(f"== {name}", flush=True)
            started = time.monotonic()
            status, note, report = "passed", "", ""
            try:
                done = await STEPS[name](editor, ctx)
                note, report = done.note, done.report
                pngs += done.pngs
            except (StepFailed, asyncio.TimeoutError, TimeoutError) as error:
                status, note = "FAILED", str(error)
                report = getattr(error, "report", None) or ""
            seconds = time.monotonic() - started
            rows.append((name, status, seconds, report))
            print(f"   {status} in {seconds:.0f}s: {note}" + (f"\n   report: {report}" if report else ""), flush=True)
            if status != "passed":
                failures.append((name, note))
                break  # later steps depend on earlier ones (compiled code, a current build)

    skipped = steps[len(rows):]
    table = ["| step | status | seconds | report |", "|------|--------|---------|--------|"]
    table += [f"| {n} | {s} | {t:.0f} | {r} |" for n, s, t, r in rows]
    table += [f"| {n} | not run | | |" for n in skipped]
    text = [f"# Validation {out.name}", "", *table, ""]
    if failures:
        text += ["## Failure", "", f"**{failures[0][0]}**", "", "```", failures[0][1], "```", ""]
    if pngs:
        text += ["## Screenshots to review", "", *[f"- {p}" for p in pngs], ""]
    (out / "summary.md").write_text("\n".join(text))
    print("\n" + "\n".join(table))
    if pngs:
        print(f"\n{len(pngs)} screenshots to review (listed in summary)")
    print(f"\nSummary: {(out / 'summary.md').relative_to(ROOT)}")
    print("RESULT: " + ("FAILED at " + failures[0][0] if failures else "PASSED"))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))

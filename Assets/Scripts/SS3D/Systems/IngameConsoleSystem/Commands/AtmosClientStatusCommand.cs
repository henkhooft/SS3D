using FishNet.Connection;
using SS3D.Permissions;
using SS3D.Rendering.URP;
using SS3D.Systems.Testing;
using System;

namespace SS3D.Systems.IngameConsoleSystem.Commands
{
    /// <summary>
    /// Reports whether this process has a valid atmos GPU snapshot to render — the only
    /// process-local signal a headless (no camera, no display) client can check to confirm
    /// <see cref="SS3D.Systems.Atmospherics.Visualization.AtmosClientVisualizationBridge"/>
    /// actually applied a chunk patch. Runs entirely client-side (no server round trip), so it
    /// also works against a pure client's own <see cref="AtmosRenderContext"/> — see
    /// Documents/architecture/2026-07_atmos-client-visualization-sync.md and
    /// Documents/architecture/2026-07_multiplayer-test-harness.md.
    /// </summary>
    public class AtmosClientStatusCommand : Command
    {
        public override string ShortDescription => "Report this process's atmos GPU snapshot status";
        public override string Usage => "[assert]\n"
            + "With no arguments, reports status. With 'assert', throws (failing an automation script) if the snapshot isn't valid.\n"
            + "example: atmosclientstatus\n"
            + "example: atmosclientstatus assert";
        public override ServerRoleTypes AccessLevel => ServerRoleTypes.User;
        public override CommandType Type => CommandType.Offline;

        private record CalculatedValues(bool Assert) : ICalculatedValues;

        public override string Perform(string[] args, NetworkConnection conn = null)
        {
            if (!ReceiveCheckResponse(args, out CheckArgsResponse response, out CalculatedValues values))
            {
                return response.InvalidArgs;
            }

            bool valid = AtmosRenderContext.TryGetSnapshot(out AtmosRenderContext.Snapshot snapshot);
            string payload = valid
                ? $"mapId={snapshot.MapId} atlasBounds={snapshot.AtlasBounds}"
                : "no snapshot";

            TestSignal.Emit(this, valid ? "AtmosClientSnapshotValid" : "AtmosClientSnapshotInvalid", payload);

            if (values.Assert && !valid)
            {
                throw new InvalidOperationException(
                    "Atmos client snapshot is not valid — no chunk patches have been applied yet");
            }

            return valid
                ? $"Atmos client snapshot valid ({payload})"
                : "Atmos client snapshot not valid (no chunk patches applied yet)";
        }

        protected override CheckArgsResponse CheckArgs(string[] args)
        {
            CheckArgsResponse response = new();

            if (args.Length > 1)
            {
                return response.MakeInvalid(WrongArgsText);
            }

            if (args.Length == 1 && !args[0].Equals("assert", StringComparison.OrdinalIgnoreCase))
            {
                return response.MakeInvalid("Unknown argument; use 'assert' or no arguments");
            }

            return response.MakeValid(new CalculatedValues(args.Length == 1));
        }
    }
}

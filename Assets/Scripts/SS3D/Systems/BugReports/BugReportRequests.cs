using System;

namespace SS3D.Systems.BugReports
{
    /// <summary>
    /// The dialog's entry points that don't own it (bare <c>/bug</c> in chat, the Escape menu button) ask
    /// through here, so chat and lobby code needn't know the dialog's host.
    /// </summary>
    public static class BugReportRequests
    {
        /// <summary>Set by the dialog's host while it can open; returns false when it could not.</summary>
        public static Func<bool> OpenHandler { get; set; }

        /// <summary>Opens the bug report dialog; false when no dialog host is running (dedicated server, batch mode).</summary>
        public static bool RequestOpen() => OpenHandler != null && OpenHandler();
    }
}

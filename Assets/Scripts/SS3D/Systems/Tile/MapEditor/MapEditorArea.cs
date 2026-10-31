using FishNet.Connection;
using FishNet.Object;
using SS3D.Core;
using SS3D.Systems.Area;
using SS3D.Systems.IdAccess;
using SS3D.Systems.Tile.MapEditor.Commands;
using UnityEngine;

namespace SS3D.Systems.Tile.MapEditor
{
    public sealed partial class MapEditorSubSystem
    {
        private readonly System.Collections.Generic.Dictionary<NetworkConnection, AreaRecord> _inspectedRooms = new();

        private Vector3? _selectedAreaPosition;

        [TargetRpc]
        private void TargetRefreshAreaProperties(NetworkConnection conn)
        {
            if (_active && CurrentTool == MapEditorTool.Area && _selectedAreaPosition.HasValue)
                RpcInspectArea(_selectedAreaPosition.Value, LocalConnection);
        }

        private void OnAreaApplyRequested(Vector3 position, string roomName, string department, ulong access) =>
            RpcApplyArea(position, roomName, department, access, LocalConnection);

        [ServerRpc(RequireOwnership = false)]
        private void RpcInspectArea(Vector3 position, NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;
            if (!SubSystems.TryGet(out AreaSubSystem areas) ||
                !areas.TryGetAreaForWorldPosition(position, out AreaRecord room))
            {
                TargetToast(conn, "No room here. Enclose the room and place an APC facing into it first.");
                return;
            }
            _inspectedRooms[conn] = room;
            TargetAreaProperties(conn, position, room.DisplayName, room.ParentTag, room.DefaultRequiredAccess.Value);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcApplyArea(Vector3 position, string roomName, string department, ulong access,
            NetworkConnection conn = null)
        {
            if (!MapEditorPermissions.TryAuthorize(conn))
                return;
            if (string.IsNullOrWhiteSpace(roomName) || roomName.Length > 80 ||
                department == null || department.Length > 40)
            {
                TargetToast(conn, "Enter a room name (up to 80 characters) and department (up to 40).");
                return;
            }
            ulong known = 0;
            foreach (AccessLevel level in System.Enum.GetValues(typeof(AccessLevel)))
                known |= (ulong)level;
            if ((access & ~known) != 0)
            {
                TargetToast(conn, "Unknown access requirement.");
                return;
            }
            if (!SubSystems.TryGet(out AreaSubSystem areas) ||
                !areas.TryGetAreaForWorldPosition(position, out AreaRecord room) ||
                !_inspectedRooms.TryGetValue(conn, out AreaRecord inspected) || !ReferenceEquals(room, inspected))
            {
                TargetToast(conn, "Room changed. Select it again.");
                return;
            }
            EnsureCommandServiceBound();
            if (!_commandService.Execute(new EditAreaPropertiesCommand(areas, room,
                    roomName.Trim(), department.Trim(), access)))
                return;
            TargetSyncUndoState(conn, _commandService.UndoDepth, _commandService.RedoDepth);
            TargetToast(conn, "Room properties applied. Save the map to keep them.");
            TargetAreaProperties(conn, position, room.DisplayName, room.ParentTag, room.DefaultRequiredAccess.Value);
        }

        [TargetRpc]
        private void TargetAreaProperties(NetworkConnection conn, Vector3 position, string roomName,
            string department, ulong access)
        {
            if (_active && CurrentTool == MapEditorTool.Area)
            {
                _selectedAreaPosition = position;
                _view.ShowAreaProperties(position, roomName, department, access);
            }
        }

        // Commands stay on the server; target the original APC rather than a recyclable area id.
        private sealed class EditAreaPropertiesCommand : IMapEditorCommand
        {
            private readonly AreaSubSystem _areas;
            private readonly AreaRecord _room;
            private readonly string _beforeName, _beforeDepartment, _afterName, _afterDepartment;
            private readonly ulong _beforeAccess, _afterAccess;

            public EditAreaPropertiesCommand(AreaSubSystem areas, AreaRecord room,
                string name, string department, ulong access)
            {
                _areas = areas;
                _room = room;
                _beforeName = room.DisplayName;
                _beforeDepartment = room.ParentTag;
                _beforeAccess = room.DefaultRequiredAccess.Value;
                _afterName = name;
                _afterDepartment = department;
                _afterAccess = access;
            }

            public MapEditorCommandDto ToDto() => throw new System.NotSupportedException("Room edits are server-local commands.");
            public void Apply(MapEditorCommandContext ctx) => Set(_afterName, _afterDepartment, _afterAccess);
            public void Revert(MapEditorCommandContext ctx) => Set(_beforeName, _beforeDepartment, _beforeAccess);

            private void Set(string name, string department, ulong access)
            {
                if (!_areas.TryGetArea(_room.Id, out AreaRecord current) || current.Apc != _room.Apc)
                    return;
                _areas.RenameArea(current.Id, name);
                _areas.SetParentTag(current.Id, department);
                _areas.SetDefaultRequiredAccess(current.Id, new AccessMask(access));
            }
        }
    }
}

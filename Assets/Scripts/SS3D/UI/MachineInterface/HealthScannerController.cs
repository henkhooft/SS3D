using FishNet.Connection;
using FishNet.Object;
using SS3D.Systems.Electricity;
using SS3D.Systems.Entities;
using SS3D.Systems.Health;
using SS3D.Systems.Selection;
using UnityEngine;

namespace SS3D.UI.MachineInterface
{
    /// <summary>
    /// Reads the nearest <see cref="HumanHealthController"/> in range and reports its vitals.
    /// No ID gate — anyone can read a scan once the subject is in range.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    [RequireComponent(typeof(MachinePowerConsumer))]
    public sealed class HealthScannerController : MachineInterfaceBehaviour
    {
        [SerializeField]
        private MachinePowerConsumer _powerConsumer;

        [SerializeField]
        private float _scanRadius = 1.5f;

        [SerializeField]
        private string _title = "VITALS SCAN";

        [SerializeField]
        private string _modelLabel = "VSU-5R · anatomical scan unit";

        [SerializeField]
        private string _subtitle = "Full Body · Anatomical Layer Scan";

        public override string InterfaceId => MachineInterfaceIds.HealthScanner;

        public override void OnStartServer()
        {
            if (_powerConsumer == null)
            {
                TryGetComponent(out _powerConsumer);
            }

            base.OnStartServer();
        }

        protected override bool ApplyControl(byte controlId, bool value)
        {
            return false;
        }

        protected override void SendOpenToViewer(NetworkConnection conn)
        {
            TargetOpenInterface(conn, BuildSnapshot());
        }

        protected override void SendRefreshToViewer(NetworkConnection conn)
        {
            TargetRefreshInterface(conn, BuildSnapshot());
        }

        [TargetRpc(RunLocally = true)]
        private void TargetOpenInterface(NetworkConnection conn, HealthScannerInterfaceSnapshot snapshot)
        {
            DispatchClientOpen(snapshot);
        }

        [TargetRpc(RunLocally = true)]
        private void TargetRefreshInterface(NetworkConnection conn, HealthScannerInterfaceSnapshot snapshot)
        {
            DispatchClientRefresh(snapshot);
        }

        private HealthScannerInterfaceSnapshot BuildSnapshot()
        {
            bool powerOk = PowerGate.IsPowered(_powerConsumer, NullConsumerPolicy.Allow);

            HealthScannerInterfaceSnapshot snapshot = new()
            {
                MachineObjectId = NetworkObject != null ? NetworkObject.ObjectId : 0,
                InterfaceId = InterfaceId,
                Title = _title,
                ModelLabel = _modelLabel,
                Subtitle = _subtitle,
                PowerOk = powerOk,
            };

            if (!powerOk)
            {
                return snapshot;
            }

            HumanHealthController target = FindNearestSubject();
            if (target == null)
            {
                return snapshot;
            }

            HealthSnapshot healthSnapshot = target.Snapshot;
            HealthDebugDetail detail = target.DebugDetail;

            snapshot.HasSubject = true;
            snapshot.SubjectName = ResolveSubjectName(target);
            snapshot.HealthState = (byte)healthSnapshot.State;
            snapshot.IsConscious = healthSnapshot.IsConscious;
            snapshot.IsCardiacArrest = healthSnapshot.IsCardiacArrest;
            snapshot.CanDefibrillate = healthSnapshot.CanDefibrillate;

            snapshot.HeadBrute = detail.Head.Brute;
            snapshot.HeadBurn = detail.Head.Burn;
            snapshot.ChestBrute = detail.Chest.Brute;
            snapshot.ChestBurn = detail.Chest.Burn;
            snapshot.LeftArmBrute = detail.LeftArm.Brute;
            snapshot.LeftArmBurn = detail.LeftArm.Burn;
            snapshot.RightArmBrute = detail.RightArm.Brute;
            snapshot.RightArmBurn = detail.RightArm.Burn;
            snapshot.LeftLegBrute = detail.LeftLeg.Brute;
            snapshot.LeftLegBurn = detail.LeftLeg.Burn;
            snapshot.RightLegBrute = detail.RightLeg.Brute;
            snapshot.RightLegBurn = detail.RightLeg.Burn;
            snapshot.GroinBrute = detail.Groin.Brute;
            snapshot.GroinBurn = detail.Groin.Burn;

            snapshot.SeveredZoneMask = healthSnapshot.SeveredZoneMask;
            snapshot.BleedingZoneMask = healthSnapshot.BleedingZoneMask;

            snapshot.BrainFunctionPercent = detail.Brain.FunctionPercent;
            snapshot.HeartFunctionPercent = detail.Heart.FunctionPercent;
            snapshot.LeftLungFunctionPercent = detail.LeftLung.FunctionPercent;
            snapshot.RightLungFunctionPercent = detail.RightLung.FunctionPercent;
            snapshot.LiverFunctionPercent = detail.Liver.FunctionPercent;

            snapshot.BloodVolumeRatio = healthSnapshot.Pools.BloodVolumeRatio;
            snapshot.OxyDebt = healthSnapshot.Pools.OxyDebt;
            snapshot.ToxinConcentration = healthSnapshot.Pools.ToxinConcentration;

            return snapshot;
        }

        private HumanHealthController FindNearestSubject()
        {
            HumanHealthController[] candidates = FindObjectsByType<HumanHealthController>(FindObjectsSortMode.None);
            float bestSqrDistance = _scanRadius * _scanRadius;
            HumanHealthController best = null;

            for (int i = 0; i < candidates.Length; i++)
            {
                HumanHealthController candidate = candidates[i];
                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance > bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            return best;
        }

        private static string ResolveSubjectName(HumanHealthController target)
        {
            if (target.TryGetComponent(out Entity entity) && entity.Mind?.player != null)
            {
                return entity.Mind.player.Ckey;
            }

            return target.gameObject.name;
        }
    }
}

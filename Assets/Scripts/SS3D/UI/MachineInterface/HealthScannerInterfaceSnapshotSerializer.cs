using FishNet.Serializing;

namespace SS3D.UI.MachineInterface
{
    public static class HealthScannerInterfaceSnapshotSerializer
    {
        public static void WriteHealthScannerInterfaceSnapshot(this Writer writer, HealthScannerInterfaceSnapshot snapshot)
        {
            writer.WriteInt32(snapshot.MachineObjectId);
            writer.WriteString(snapshot.InterfaceId);
            writer.WriteString(snapshot.Title);
            writer.WriteString(snapshot.ModelLabel);
            writer.WriteString(snapshot.Subtitle);
            writer.WriteBoolean(snapshot.PowerOk);

            writer.WriteBoolean(snapshot.HasSubject);
            writer.WriteString(snapshot.SubjectName);
            writer.WriteByte(snapshot.HealthState);
            writer.WriteBoolean(snapshot.IsConscious);
            writer.WriteBoolean(snapshot.IsCardiacArrest);
            writer.WriteBoolean(snapshot.CanDefibrillate);

            writer.WriteSingle(snapshot.HeadBrute);
            writer.WriteSingle(snapshot.HeadBurn);
            writer.WriteSingle(snapshot.ChestBrute);
            writer.WriteSingle(snapshot.ChestBurn);
            writer.WriteSingle(snapshot.LeftArmBrute);
            writer.WriteSingle(snapshot.LeftArmBurn);
            writer.WriteSingle(snapshot.RightArmBrute);
            writer.WriteSingle(snapshot.RightArmBurn);
            writer.WriteSingle(snapshot.LeftLegBrute);
            writer.WriteSingle(snapshot.LeftLegBurn);
            writer.WriteSingle(snapshot.RightLegBrute);
            writer.WriteSingle(snapshot.RightLegBurn);
            writer.WriteSingle(snapshot.GroinBrute);
            writer.WriteSingle(snapshot.GroinBurn);

            writer.WriteInt32(snapshot.SeveredZoneMask);
            writer.WriteInt32(snapshot.BleedingZoneMask);

            writer.WriteSingle(snapshot.BrainFunctionPercent);
            writer.WriteSingle(snapshot.HeartFunctionPercent);
            writer.WriteSingle(snapshot.LeftLungFunctionPercent);
            writer.WriteSingle(snapshot.RightLungFunctionPercent);
            writer.WriteSingle(snapshot.LiverFunctionPercent);

            writer.WriteSingle(snapshot.BloodVolumeRatio);
            writer.WriteSingle(snapshot.OxyDebt);
            writer.WriteSingle(snapshot.ToxinConcentration);
        }

        public static HealthScannerInterfaceSnapshot ReadHealthScannerInterfaceSnapshot(this Reader reader)
        {
            return new HealthScannerInterfaceSnapshot
            {
                MachineObjectId = reader.ReadInt32(),
                InterfaceId = reader.ReadString(),
                Title = reader.ReadString(),
                ModelLabel = reader.ReadString(),
                Subtitle = reader.ReadString(),
                PowerOk = reader.ReadBoolean(),

                HasSubject = reader.ReadBoolean(),
                SubjectName = reader.ReadString(),
                HealthState = reader.ReadByte(),
                IsConscious = reader.ReadBoolean(),
                IsCardiacArrest = reader.ReadBoolean(),
                CanDefibrillate = reader.ReadBoolean(),

                HeadBrute = reader.ReadSingle(),
                HeadBurn = reader.ReadSingle(),
                ChestBrute = reader.ReadSingle(),
                ChestBurn = reader.ReadSingle(),
                LeftArmBrute = reader.ReadSingle(),
                LeftArmBurn = reader.ReadSingle(),
                RightArmBrute = reader.ReadSingle(),
                RightArmBurn = reader.ReadSingle(),
                LeftLegBrute = reader.ReadSingle(),
                LeftLegBurn = reader.ReadSingle(),
                RightLegBrute = reader.ReadSingle(),
                RightLegBurn = reader.ReadSingle(),
                GroinBrute = reader.ReadSingle(),
                GroinBurn = reader.ReadSingle(),

                SeveredZoneMask = reader.ReadInt32(),
                BleedingZoneMask = reader.ReadInt32(),

                BrainFunctionPercent = reader.ReadSingle(),
                HeartFunctionPercent = reader.ReadSingle(),
                LeftLungFunctionPercent = reader.ReadSingle(),
                RightLungFunctionPercent = reader.ReadSingle(),
                LiverFunctionPercent = reader.ReadSingle(),

                BloodVolumeRatio = reader.ReadSingle(),
                OxyDebt = reader.ReadSingle(),
                ToxinConcentration = reader.ReadSingle(),
            };
        }
    }
}

namespace SS3D.UI.MachineInterface
{
    public struct HealthScannerInterfaceSnapshot
    {
        public int MachineObjectId;

        public string InterfaceId;

        public string Title;

        public string ModelLabel;

        public string Subtitle;

        public bool PowerOk;

        public bool HasSubject;

        public string SubjectName;

        public byte HealthState;

        public bool IsConscious;

        public bool IsCardiacArrest;

        public bool CanDefibrillate;

        public float HeadBrute;

        public float HeadBurn;

        public float ChestBrute;

        public float ChestBurn;

        public float LeftArmBrute;

        public float LeftArmBurn;

        public float RightArmBrute;

        public float RightArmBurn;

        public float LeftLegBrute;

        public float LeftLegBurn;

        public float RightLegBrute;

        public float RightLegBurn;

        public float GroinBrute;

        public float GroinBurn;

        public int SeveredZoneMask;

        public int BleedingZoneMask;

        public float BrainFunctionPercent;

        public float HeartFunctionPercent;

        public float LeftLungFunctionPercent;

        public float RightLungFunctionPercent;

        public float LiverFunctionPercent;

        public float BloodVolumeRatio;

        public float OxyDebt;

        public float ToxinConcentration;
    }
}

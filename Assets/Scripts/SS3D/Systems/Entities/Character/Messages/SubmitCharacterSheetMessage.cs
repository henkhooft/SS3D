using System;
using FishNet.Broadcast;

namespace SS3D.Systems.Entities.Character.Messages
{
    /// <summary>
    /// Client submits session character customization before Ready/Embark.
    /// </summary>
    [Serializable]
    public struct SubmitCharacterSheetMessage : IBroadcast
    {
        public string Ckey;
        public string Name;
        public int HairStyleId;
        public int BeardStyleId;
        public int SkinToneIndex;
        public int HairColorIndex;

        public SubmitCharacterSheetMessage(string ckey, CharacterSheet sheet)
        {
            Ckey = ckey;
            Name = sheet.Name;
            HairStyleId = sheet.HairStyleId;
            BeardStyleId = sheet.BeardStyleId;
            SkinToneIndex = sheet.SkinToneIndex;
            HairColorIndex = sheet.HairColorIndex;
        }

        public CharacterSheet ToSheet()
        {
            return new CharacterSheet
            {
                Name = Name,
                HairStyleId = HairStyleId,
                BeardStyleId = BeardStyleId,
                SkinToneIndex = SkinToneIndex,
                HairColorIndex = HairColorIndex,
            };
        }
    }
}

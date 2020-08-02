﻿using Mirror;
using UnityEngine;

namespace SS3D.Engine.Health
{
    public class BloodSystem : NetworkBehaviour
    {
        /// <summary>
        /// How much toxin is found in the blood. 0% to 100%
        /// </summary>
        public float ToxinLevel
        {
            get { return Mathf.Clamp(toxinLevel, 0, 101); }
            set { toxinLevel = Mathf.Clamp(value, 0, 101); }
        }

        /// <summary>
        /// The lack of oxygen levels found in the blood.
        /// </summary>
        public float OxygenDamage
        {
            get { return Mathf.Clamp(oxygenDamage, 0, 200); }
            set { oxygenDamage = Mathf.Clamp(value, 0, 200); }
        }

        /// <summary>
        /// The heart rate affects the rate at which blood is pumped around the body
        /// This is only relevant on the Server.
        /// HeartRate value can be requested by a client via a NetMsg
        /// </summary>
        /// <value>Measured in BPM</value>
        public int HeartRate { get; set; } = 55; //Resting is 55. 0 = dead

        /// <summary>
        /// Is the Heart Stopped. Performing CPR might start it again
        /// </summary>
        public bool HeartStopped => HeartRate == 0;

        private float oxygenDamage = 0;
        private float toxinLevel = 0;

        private CreatureHealth creatureHealth;
        private DNABloodType bloodType;
        private readonly float bleedRate = 2f;
        public float BloodLevel = (int)BloodVolume.Normal;
        public bool IsBleeding { get; private set; }
        private float tickRate = 1f;
        private float tick = 0f;

        private BloodSplatType bloodSplatColor;

        void Start()
        {
            creatureHealth = GetComponent<CreatureHealth>();
        }

        //Initial setting for blood type. Server only
        [Server]
        public void SetBloodType(DNABloodType dnaBloodType)
        {
            bloodType = dnaBloodType;
            bloodSplatColor = dnaBloodType.BloodColor;
        }

        public void Update()
        {
            // server only
            if (isServer)
            {
                if (creatureHealth.IsDead)
                {
                    HeartRate = 0;
                    return;
                }

                tick += Time.deltaTime;
                if (HeartRate == 0)
                {
                    tick = 0;
                    return;
                }

                if (tick >= 60f / (float)HeartRate) //Heart rate determines loop time
                {
                    tick = 0f;
                    PumpBlood();
                }
            }
        }

        /// <summary>
        /// Where the blood pumping action happens
        /// </summary>
        void PumpBlood()
        {
            if (IsBleeding)
            {
                float bleedVolume = 0;
                for (int i = 0; i < creatureHealth.BodyParts.Count; i++)
                {
                    BodyPartBehaviour BPB = creatureHealth.BodyParts[i];
                    if (BPB.isBleeding)
                    {
                        bleedVolume += (BPB.BruteDamage * 0.013f);
                    }
                }
                LoseBlood(bleedVolume);
            }

            //TODO things that could affect heart rate, like low blood, crit status etc
        }

        /// <summary>
        /// Subtract an amount of blood from the player. Server Only
        /// </summary>
        [Server]
        public void AddBloodLoss(int amount, BodyPartBehaviour bodyPart)
        {
            if (amount <= 0)
            {
                return;
            }
            TryBleed(bodyPart);
        }

        private void TryBleed(BodyPartBehaviour bodyPart)
        {
            bodyPart.isBleeding = true;

            //don't start another coroutine when already bleeding
            if (!IsBleeding)
            {
                IsBleeding = true;
            }
        }

        /// <summary>
        /// Stops bleeding on the selected body part. The blood system continues bleeding if there's another bodypart bleeding. Server Only.
        /// </summary>
        [Server]
        public void StopBleeding(BodyPartBehaviour bodyPart)
        {
            bodyPart.isBleeding = false;
            for (int i = 0; i < creatureHealth.BodyParts.Count; i++)
            {
                BodyPartBehaviour bpb = creatureHealth.BodyParts[i];
                if (bpb.isBleeding)
                {
                    return;
                }
            }
            IsBleeding = false;
        }

        /// <summary>
        /// Stops bleeding on all body parts. Server Only.
        /// </summary>
        public void StopBleedingAll()
        {
            for (int i = 0; i < creatureHealth.BodyParts.Count; i++)
            {
                BodyPartBehaviour bpb = creatureHealth.BodyParts[i];
                bpb.isBleeding = false;
            }
            IsBleeding = false;
        }

        private void LoseBlood(float amount)
        {
            if (amount <= 0)
            {
                return;
            }

            BloodLevel -= amount;
            BloodSplatSize scaleOfTragedy;
            if (amount > 0 && amount < 15)
            {
                scaleOfTragedy = BloodSplatSize.small;
            }
            else if (amount >= 15 && amount < 40)
            {
                scaleOfTragedy = BloodSplatSize.medium;
            }
            else
            {
                scaleOfTragedy = BloodSplatSize.large;
            }

            // TODO add blood effect
        }

        /// <summary>
        /// Restore blood level
        /// </summary>
        private void RestoreBlood()
        {
            BloodLevel = (int)BloodVolume.Normal;
        }

        private static float BleedFactor(DamageType damageType)
        {
            float random = Random.Range(-0.2f, 0.2f);
            switch (damageType)
            {
                case DamageType.Brute:
                    return 0.6f + random;
                case DamageType.Burn:
                    return 0.4f + random;
                case DamageType.Toxic:
                    return 0.2f + random;
            }
            return 0;
        }

        /// <summary>
        /// Determine if there is any blood damage (toxin, oxygen loss) or bleeding that needs to occur
        /// Server only!
        /// </summary>
        public void AffectBloodState(BodyPartType bodyPartType, DamageType damageType, float amount, bool isHeal = false)
        {
            BodyPartBehaviour bodyPart = creatureHealth.FindBodyPart(bodyPartType);

            if (isHeal)
            {
                CheckHealing(bodyPart, damageType, amount);
                return;
            }

            //Check if limb should start bleeding (Bleeding is only for Players, not animals)
            if (damageType == DamageType.Brute)
            {
                int bloodLoss = (int)(Mathf.Clamp(amount, 0f, 10f) * BleedFactor(damageType));
                // start bleeding if the limb is really damaged
                if (bodyPart.BruteDamage > 40)
                {
                    AddBloodLoss(bloodLoss, bodyPart);
                }
            }

            if (damageType == DamageType.Toxic)
            {
                ToxinLevel += amount;
            }
        }

        //Do any healing stuff
        private void CheckHealing(BodyPartBehaviour bodyPart, DamageType damageType, float healAmt)
        {
            //TODO: PRIORITY! Do Blood healing!
        }

        // --------------------
        // UPDATES FROM SERVER
        // --------------------
        public void UpdateClientBloodStats(int heartRate, float bloodVolume, float _oxygenDamage, float _toxinLevel)
        {
            if (isServer)
            {
                return;
            }

            HeartRate = heartRate;
            BloodLevel = bloodVolume;
            OxygenDamage = _oxygenDamage;
            toxinLevel = _toxinLevel;
        }
    }
}
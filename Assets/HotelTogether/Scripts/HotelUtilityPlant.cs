using UnityEngine;

namespace HotelTogether
{
    // Runs after maintenance builds the shared tool rack and incident registry.
    [DefaultExecutionOrder(160)]
    public sealed class HotelUtilityPlant : MonoBehaviour
    {
        public HotelMaintenanceIncident electrical, pump;
        public Transform generatorRotor, pumpRotor, pressureNeedle;
        public Light[] emergencyLights;
        public TextMesh powerStatus, waterStatus;
        HotelMaintenanceSystem system;
        AudioSource generatorSound, pumpSound;
        AudioClip motorHum;
        float pressure;

        void Start()
        {
            system = FindAnyObjectByType<HotelMaintenanceSystem>();
            if (!system) return;
            if (!system.Initialized) system.Initialize(system.GetComponent<HotelGuestSimulation>());
            foreach (var old in system.incidents.ToArray())
                if (old && old.kind == MaintenanceIncidentKind.PowerOutage && old != electrical)
                {
                    system.incidents.Remove(old);
                    old.gameObject.SetActive(false);
                }
            foreach (var site in new[] { electrical, pump })
                if (site)
                {
                    site.system = system;
                    if (!system.incidents.Contains(site)) system.incidents.Add(site);
                }
            if (emergencyLights != null)
                foreach (var light in emergencyLights) system.RegisterEmergency(light);

            motorHum = AudioClip.Create("Utility motor hum", 24000, 1, 24000, false);
            var samples = new float[24000];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / 24000f;
                samples[i] = (Mathf.Sin(t * Mathf.PI * 160) * .4f + Mathf.Sin(t * Mathf.PI * 320) * .2f + Mathf.Sin(t * Mathf.PI * 960) * .1f) * (1 + .1f * Mathf.Sin(t * Mathf.PI * 20));
            }
            motorHum.SetData(samples, 0);
            generatorSound = MotorSound(generatorRotor, .13f, .85f);
            pumpSound = MotorSound(pumpRotor, .10f, 1.25f);
        }

        AudioSource MotorSound(Transform rotor, float volume, float pitch)
        {
            if (!rotor) return null;
            var source = rotor.gameObject.AddComponent<AudioSource>();
            source.clip = motorHum; source.loop = true; source.playOnAwake = false;
            source.spatialBlend = 1; source.minDistance = 1; source.maxDistance = 10;
            source.volume = volume; source.pitch = pitch;
            return source;
        }

        static void SetMotorSound(AudioSource source, bool running)
        {
            if (!source) return;
            if (running && !source.isPlaying) source.Play();
            else if (!running && source.isPlaying) source.Stop();
        }

        void Update()
        {
            bool running = system && system.hotel && system.hotel.Running;
            SetMotorSound(generatorSound, running && system.HasPower);
            SetMotorSound(pumpSound, running && system.WaterAvailable);
            if (!running) return;
            if (generatorRotor && system.HasPower) generatorRotor.Rotate(Vector3.forward, Time.deltaTime * 180, Space.Self);
            if (pumpRotor && system.WaterAvailable) pumpRotor.Rotate(Vector3.forward, Time.deltaTime * 240, Space.Self);
            pressure = Mathf.MoveTowards(pressure, system.WaterAvailable ? 1 : 0, Time.deltaTime * .55f);
            if (pressureNeedle) pressureNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(65, -65, pressure));
            string powerText = system.HasPower ? "POWER ONLINE" : "POWER ISOLATED";
            string waterText = !system.HasPower ? "PUMP: NO POWER" : system.HasWater ? "PRESSURE NORMAL" : "PRESSURE LOST";
            if (powerStatus && powerStatus.text != powerText) powerStatus.text = powerText;
            if (waterStatus && waterStatus.text != waterText) waterStatus.text = waterText;
        }

        void OnDisable() { SetMotorSound(generatorSound, false); SetMotorSound(pumpSound, false); }
        void OnDestroy() { if (motorHum) Destroy(motorHum); }
    }
}

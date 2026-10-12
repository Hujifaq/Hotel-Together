using UnityEngine;

namespace HotelTogether
{
    [DisallowMultipleComponent, RequireComponent(typeof(HotelItem))]
    public sealed class HotelWaterFixture : MonoBehaviour
    {
        public bool shower;
        public Vector3 outlet = new Vector3(0, .925f, .01f);
        public Vector3 impact = new Vector3(0, .765f, .01f);
        public Material streamMaterial, dropletMaterial;
        public AudioClip waterSound;
        public float volume = .28f;
        public bool IsRunning { get; private set; }
        public int LiveParticles => (drops ? drops.particleCount : 0) + (splashes ? splashes.particleCount : 0);
        ParticleSystem drops, splashes;
        LineRenderer stream;
        readonly LineRenderer[] ripples = new LineRenderer[2];
        readonly Vector3[] ripplePoints = new Vector3[33];
        readonly Vector3[] streamPoints = new Vector3[9];
        AudioSource audioSource;
        bool initialized;

        void Awake() { GetComponent<HotelItem>().waterFixture = this; }
        public void Toggle() { SetRunning(!IsRunning); }
        public void SetRunning(bool running)
        {
            if (running && !isActiveAndEnabled) return;
            if (running && !initialized) Initialize();
            IsRunning = running;
            if (!initialized) return;
            if (stream) stream.enabled = running;
            foreach (var ring in ripples) if (ring) ring.enabled = running;
            if (running)
            {
                drops.Play(); splashes.Play();
                if (waterSound && !audioSource.isPlaying) { audioSource.volume = 0; audioSource.Play(); }
            }
            else
            {
                drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                audioSource.Stop();
            }
        }
        void Initialize()
        {
            initialized = true;
            var plane = Child("Water impact plane", impact);
            drops = Particles("Falling water droplets", outlet, false);
            var collision = drops.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, plane); collision.bounce = 0; collision.dampen = 1; collision.lifetimeLoss = 1;
            splashes = Particles("Water impact splashes", impact + Vector3.up * .005f, true);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.clip = waterSound; audioSource.loop = true;
            audioSource.playOnAwake = false; audioSource.spatialBlend = 1; audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = .8f; audioSource.maxDistance = shower ? 6 : 4; audioSource.dopplerLevel = 0;
            if (!shower)
            {
                stream = Line("Continuous tap stream", 9, .011f);
                stream.endWidth = .016f;
            }
            for (int i = 0; i < 2; i++) ripples[i] = Line("Water surface ripple " + i, 33, .002f);
        }
        Transform Child(string name, Vector3 position)
        {
            var g = new GameObject(name); g.transform.SetParent(transform, false); g.transform.localPosition = position; return g.transform;
        }
        LineRenderer Line(string name, int points, float width)
        {
            var line = Child(name, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.positionCount = points; line.widthMultiplier = width;
            line.sharedMaterial = streamMaterial; line.startColor = new Color(.74f,.91f,1,.65f); line.endColor = new Color(.8f,.94f,1,.4f);
            line.numCapVertices = 3; line.enabled = false; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        ParticleSystem Particles(string name, Vector3 position, bool splash)
        {
            var t = Child(name, position); t.localRotation = Quaternion.Euler(splash ? -90 : 90, 0, 0);
            var ps = t.gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = splash ? 120 : shower ? 900 : 60;
            main.startLifetime = splash ? new ParticleSystem.MinMaxCurve(.12f,.24f) : shower ? new ParticleSystem.MinMaxCurve(.55f,.65f) : new ParticleSystem.MinMaxCurve(.11f,.15f);
            main.startSpeed = splash ? new ParticleSystem.MinMaxCurve(.22f,.65f) : shower ? new ParticleSystem.MinMaxCurve(2.7f,3.2f) : new ParticleSystem.MinMaxCurve(.7f,1.1f);
            main.startSize = splash ? new ParticleSystem.MinMaxCurve(.003f,.008f) : new ParticleSystem.MinMaxCurve(.003f,.006f);
            main.gravityModifier = splash ? .8f : .35f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.65f,.85f,.97f,.30f),new Color(.93f,.98f,1,.65f));
            var emission = ps.emission; emission.rateOverTime = splash ? (shower ? 190 : 45) : shower ? 620 : 65;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = splash ? 55f : 1f; shape.length = .01f;
            shape.radius = splash ? (shower ? .13f : .025f) : shower ? .095f : .003f;
            shape.randomDirectionAmount = splash ? .7f : .015f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = dropletMaterial;
            renderer.renderMode = splash ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f; renderer.velocityScale = .025f; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!splash && shower)
            {
                var noise = ps.noise; noise.enabled = true; noise.strength = .007f; noise.frequency = 4; noise.scrollSpeed = .4f;
                noise.quality = ParticleSystemNoiseQuality.Low;
            }
            return ps;
        }
        void Update()
        {
            if (!IsRunning || !initialized) return;
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, volume, Time.deltaTime * .8f);
            if (stream)
            {
                for (int i = 0; i < streamPoints.Length; i++)
                {
                    float t = i / 8f; streamPoints[i] = Vector3.Lerp(outlet, impact, t);
                    streamPoints[i].x += Mathf.Sin(Time.time * 27 + t * 14) * .0012f * t;
                }
                stream.SetPositions(streamPoints);
            }
            for (int ring = 0; ring < 2; ring++)
            {
                float phase = Mathf.Repeat(Time.time * 1.8f + ring * .5f, 1);
                float radius = Mathf.Lerp(.008f, shower ? .18f : .07f, phase);
                for (int i = 0; i < 33; i++)
                {
                    float a = i / 32f * Mathf.PI * 2;
                    ripplePoints[i] = impact + new Vector3(Mathf.Cos(a)*radius,.006f,Mathf.Sin(a)*radius);
                }
                ripples[ring].SetPositions(ripplePoints);
                var color = new Color(.72f,.87f,.94f,(1-phase)*.30f); ripples[ring].startColor = color; ripples[ring].endColor = color;
            }
        }
        void OnDisable() { SetRunning(false); }
    }
}

using UnityEngine;
using UnityEngine.AI;

namespace Rambler.DebugTools
{
    //Dev tool: add to a character during play to measure foot sliding and teleporting.
    //Planted feet (lowest few cm of their range) should stay still in the world; anything they move is slide.
    //A teleport is a frame where the character moves far more than its agent speed allows, or turns very sharply.
    public class MotionProbe : MonoBehaviour
    {
        [Tooltip("A foot counts as planted when it is within this height of its lowest point (metres).")]
        public float plantBand = 0.04f;

        Animator anim;
        NavMeshAgent agent;
        Transform lFoot, rFoot;
        Vector3 lastPos, lastL, lastR;
        float lastYaw;
        float lMin = 99f, rMin = 99f;
        bool started;

        //Results since Begin()
        public int frames, teleports, snapTurns;
        public float maxStep, maxTurn, slideDistance, plantedTime, maxSlideSpeed, movingTime, travelled;
        public string lastTeleport = "";

        void Awake()
        {
            anim = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
            if (anim != null && anim.isHuman)
            {
                lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            Begin();
        }

        public void Begin()
        {
            frames = teleports = snapTurns = 0;
            maxStep = maxTurn = slideDistance = plantedTime = maxSlideSpeed = movingTime = travelled = 0f;
            lastTeleport = "";
            started = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 pos = transform.position;
            float yaw = transform.eulerAngles.y;
            Vector3 l = lFoot != null ? lFoot.position : pos, r = rFoot != null ? rFoot.position : pos;
            if (!started)
            {
                started = true; lastPos = pos; lastYaw = yaw; lastL = l; lastR = r;
                return;
            }
            frames++;

            //Teleports and snap turns
            Vector3 step = pos - lastPos; step.y = 0f;
            float allowed = Mathf.Max(agent != null ? agent.speed : 6f, 1f) * dt * 2f + 0.15f;
            if (step.magnitude > maxStep) maxStep = step.magnitude;
            if (step.magnitude > allowed)
            {
                teleports++;
                lastTeleport = "t=" + Time.time.ToString("0.00") + " moved " + step.magnitude.ToString("0.00") + "m in one frame (allowed " + allowed.ToString("0.00") + ")";
            }
            float turn = Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
            if (turn > maxTurn) maxTurn = turn;
            if (turn > 30f) snapTurns++;
            travelled += step.magnitude;
            if (agent != null && agent.velocity.magnitude > 0.2f) movingTime += dt;

            //Foot slide: horizontal movement of a planted foot
            if (lFoot != null)
            {
                lMin = Mathf.Min(lMin, l.y - pos.y); rMin = Mathf.Min(rMin, r.y - pos.y);
                Slide(l, lastL, lMin, pos, dt);
                Slide(r, lastR, rMin, pos, dt);
            }
            lastPos = pos; lastYaw = yaw; lastL = l; lastR = r;
        }

        void Slide(Vector3 foot, Vector3 last, float minH, Vector3 root, float dt)
        {
            if (foot.y - root.y > minH + plantBand) return;
            Vector3 d = foot - last; d.y = 0f;
            slideDistance += d.magnitude;
            plantedTime += dt;
            float speed = d.magnitude / dt;
            if (speed > maxSlideSpeed) maxSlideSpeed = speed;
        }

        public string Report()
        {
            float avgSlide = plantedTime > 0f ? slideDistance / plantedTime : 0f;
            return name + ": frames=" + frames + " moving=" + movingTime.ToString("0.0") + "s travelled=" + travelled.ToString("0.0") + "m" +
                   " | planted-foot slide avg " + avgSlide.ToString("0.00") + " m/s (max " + maxSlideSpeed.ToString("0.00") + ")" +
                   " | teleports=" + teleports + " maxStep=" + maxStep.ToString("0.00") + "m" + (lastTeleport != "" ? " [" + lastTeleport + "]" : "") +
                   " | snapTurns(>30deg/frame)=" + snapTurns + " maxTurn=" + maxTurn.ToString("0") + "deg";
        }
    }
}

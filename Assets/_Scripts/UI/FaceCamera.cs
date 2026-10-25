using UnityEngine;

namespace Rambler.UI
{
    //Billboard: keeps a world space UI element square-on to the camera as it orbits
    public class FaceCamera : MonoBehaviour
    {
        Transform cam;

        void LateUpdate()
        {
            if (cam == null)
            {
                if (Camera.main == null) return;
                cam = Camera.main.transform;
            }
            transform.rotation = cam.rotation;
        }
    }
}

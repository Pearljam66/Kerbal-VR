using UnityEngine;

namespace KerbalVR
{
    public class Types
    {
        // enumerates the different KSP camera types
        public enum KspCamera {
            INTERNAL_CAMERA = 0,
            CAMERA_00 = 1,
            CAMERA_01 = 2,
            SCALED_SPACE = 3,
            GALAXY = 4,
            SCENERY = 5,
            MAIN = 6,
            MARKER = 7,
            KSP_CAMERA_ENUM_MAX,
        }
    }
}

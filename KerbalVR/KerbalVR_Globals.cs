using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KerbalVR
{
    /// <summary>
    /// A class to contain globally accessible constants.
    /// </summary>
    public class Globals
    {
        // path to the KerbalVR Assets directory
        public static readonly string KERBALVR_ASSETS_DIR = "KerbalVR/Assets/";

        // define location of OpenVR library
        public static string OpenVRDllPath {
            get {
                string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string openVrPath = Path.Combine(currentPath, "openvr");
                return Path.Combine(openVrPath, Utils.Is64BitProcess ? "win64" : "win32");
            }
        }

        // a prefix to append to every KerbalVR debug log message
        public static readonly string LOG_PREFIX = "[KerbalVR] ";

        public static readonly string[] FLIGHT_SCENE_CAMERAS = {
            "GalaxyCamera",
            "Camera ScaledSpace",
            "Camera 01",
            "Camera 00",
            "InternalCamera",
        };
    }
}
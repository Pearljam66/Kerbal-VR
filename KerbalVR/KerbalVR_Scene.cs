using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Valve.VR;

namespace KerbalVR
{
    /// <summary>
    /// Scene is a singleton class that encapsulates the code that positions
    /// the game cameras correctly for rendering them to the VR headset,
    /// according to the current KSP scene (flight, editor, etc).
    /// </summary>
    public class Scene : MonoBehaviour
    {
        #region Constants
        private static readonly Types.KspCamera[] WORLD_SPACE_CAMERAS = {
            Types.KspCamera.GALAXY,
            Types.KspCamera.SCALED_SPACE,
            Types.KspCamera.CAMERA_01,
            Types.KspCamera.CAMERA_00,
        };

        private static readonly Types.KspCamera[] FLIGHT_SCENE_IVA_CAMERAS = {
            Types.KspCamera.GALAXY,
            Types.KspCamera.SCALED_SPACE,
            Types.KspCamera.CAMERA_01,
            Types.KspCamera.CAMERA_00,
            Types.KspCamera.INTERNAL_CAMERA,
        };

        private static readonly Types.KspCamera[] FLIGHT_SCENE_EVA_CAMERAS = {
            Types.KspCamera.GALAXY,
            Types.KspCamera.SCALED_SPACE,
            Types.KspCamera.CAMERA_01,
            Types.KspCamera.CAMERA_00,
        };

        private static readonly Types.KspCamera[] SPACECENTER_SCENE_CAMERAS = {
            Types.KspCamera.GALAXY,
            Types.KspCamera.SCALED_SPACE,
            Types.KspCamera.CAMERA_01,
            Types.KspCamera.CAMERA_00,
        };

        private static readonly Types.KspCamera[] EDITOR_SCENE_CAMERAS = {
            Types.KspCamera.GALAXY,
            Types.KspCamera.SCENERY,
            Types.KspCamera.MAIN,
            Types.KspCamera.MARKER,
        };

        private static readonly Dictionary<Types.KspCamera, string> KSP_CAMERA_NAMES = new Dictionary<Types.KspCamera, string>() {
            { Types.KspCamera.INTERNAL_CAMERA, "InternalCamera" },
            { Types.KspCamera.CAMERA_00, "Camera 00" },
            { Types.KspCamera.CAMERA_01, "Camera 01" },
            { Types.KspCamera.SCALED_SPACE, "Camera ScaledSpace" },
            { Types.KspCamera.GALAXY, "GalaxyCamera" },
            { Types.KspCamera.SCENERY, "sceneryCam" },
            { Types.KspCamera.MAIN, "Main Camera" },
            { Types.KspCamera.MARKER, "markerCam" },
        };
        #endregion

        #region Singleton
        // this is a singleton class, and there must be one Scene in the scene
        private static Scene _instance;
        public static Scene Instance {
            get {
                if (_instance == null) {
                    _instance = FindObjectOfType<Scene>();
                    if (_instance == null) {
                        Utils.LogError("The scene needs to have one active GameObject with a Scene script attached!");
                    } else {
                        _instance.Initialize();
                    }
                }
                return _instance;
            }
        }

        // first-time initialization for this singleton class
        private void Initialize() {
            HmdEyePosition = new Vector3[2];
            HmdEyeRotation = new Quaternion[2];

            // initialize world scale values
            inverseWorldScale = new Dictionary<GameScenes, float>();
            inverseWorldScale.Add(GameScenes.MAINMENU, 1f);
            inverseWorldScale.Add(GameScenes.SPACECENTER, 1f);
            inverseWorldScale.Add(GameScenes.TRACKSTATION, 1f);
            inverseWorldScale.Add(GameScenes.FLIGHT, 1f);
            inverseWorldScale.Add(GameScenes.EDITOR, 1f);

            // create the VR camera rig
            VrCameraRig = new GameObject("KVR_CameraRig");
            GameObject vrCameraEyeL = new GameObject("KVR_CameraRig_EyeL");
            vrCameraEyeL.transform.SetParent(VrCameraRig.transform);
            GameObject vrCameraEyeR = new GameObject("KVR_CameraRig_EyeR");
            vrCameraEyeR.transform.SetParent(VrCameraRig.transform);
            vrCameraRigEye.Add(EVREye.Eye_Left, vrCameraEyeL);
            vrCameraRigEye.Add(EVREye.Eye_Right, vrCameraEyeR);

            /*GameObject rigGizmo = Utils.CreateGizmo();
            rigGizmo.transform.SetParent(VrCameraRig.transform);
            GameObject leftGizmo = Utils.CreateGizmo();
            leftGizmo.transform.SetParent(vrCameraEyeL.transform);
            leftGizmo.transform.localScale = Vector3.one * 0.5f;
            GameObject rightGizmo = Utils.CreateGizmo();
            rightGizmo.transform.SetParent(vrCameraEyeR.transform);
            rightGizmo.transform.localScale = Vector3.one * 0.5f;*/

            for (int i = 0; i < 2; i++) {
                EVREye eye = (EVREye)i; // heh, get it, eye equals i ...
                for (int j = 0; j < (int)Types.KspCamera.KSP_CAMERA_ENUM_MAX; j++) {
                    Types.KspCamera cameraType = (Types.KspCamera)j;
                    GameObject eyeCamera = CreateVrCameraGameObject(eye, cameraType);
                    eyeCamera.transform.SetParent((eye == EVREye.Eye_Left) ? vrCameraEyeL.transform : vrCameraEyeR.transform);

                    vrEyeCameras[eye][j] = eyeCamera;
                }
            }

            DontDestroyOnLoad(VrCameraRig);
        }
        #endregion


        #region Properties
        /// <summary>
        /// The VR camera rig GameObject
        /// </summary>
        public GameObject VrCameraRig { get; private set; }

        // the render textures for the HMD
        private RenderTexture _hmdEyeRenderTextureL;
        public RenderTexture HmdEyeRenderTextureL {
            get {
                return _hmdEyeRenderTextureL;
            }
            set {
                _hmdEyeRenderTextureL = value;
                for (int i = 0; i < (int)Types.KspCamera.KSP_CAMERA_ENUM_MAX; i++) {
                    Camera cameraComponent = vrEyeCameras[EVREye.Eye_Left][i].GetComponent<Camera>();
                    cameraComponent.targetTexture = value;
                }
            }
        }

        private RenderTexture _hmdEyeRenderTextureR;
        public RenderTexture HmdEyeRenderTextureR {
            get {
                return _hmdEyeRenderTextureR;
            }
            set {
                _hmdEyeRenderTextureR = value;
                for (int i = 0; i < (int)Types.KspCamera.KSP_CAMERA_ENUM_MAX; i++) {
                    Camera cameraComponent = vrEyeCameras[EVREye.Eye_Right][i].GetComponent<Camera>();
                    cameraComponent.targetTexture = value;
                }
            }
        }

        // The initial world position of the cameras for the current scene. This
        // position corresponds to the origin in the real world physical device
        // coordinate system.
        public Vector3 InitialPosition { get; private set; }
        public Quaternion InitialRotation { get; private set; }

        // The current world position of the cameras for the current scene. This
        // position corresponds to the origin in the real world physical device
        // coordinate system.
        public Vector3 CurrentPosition { get; set; }
        public Quaternion CurrentRotation { get; set; }

        /// <summary>
        /// The current position of the HMD in Unity world coordinates
        /// </summary>
        public Vector3 HmdPosition { get; private set; }
        /// <summary>
        /// The current rotation of the HMD in Unity world coordinates
        /// </summary>
        public Quaternion HmdRotation { get; private set; }

        /// <summary>
        /// The current position of the HMD eye in Unity world coordinates,
        /// indexed by EVREye value.
        /// </summary>
        public Vector3[] HmdEyePosition { get; private set; }
        /// <summary>
        /// The current rotation of the HMD left eye in Unity world coordinates,
        /// indexed by EVREye value.
        /// </summary>
        public Quaternion[] HmdEyeRotation { get; private set; }

        /// <summary>
        /// Defines the tracking method to use.
        /// </summary>
        public ETrackingUniverseOrigin TrackingSpace { get; private set; }

        /// <summary>
        /// Defines what layer to render KerbalVR objects on.
        /// </summary>
        public int RenderLayer { get; private set; }

        /// <summary>
        /// Defines the world scaling factor (store the inverse).
        /// </summary>
        public float WorldScale {
            get { return (1f / inverseWorldScale[HighLogic.LoadedScene]); }
            set { inverseWorldScale[HighLogic.LoadedScene] = (1f / value); }
        }
        #endregion


        #region Private Members
        private Dictionary<GameScenes, float> inverseWorldScale;
        private float editorMovementSpeed = 1f;
        private Dictionary<EVREye, GameObject[]> vrEyeCameras = new Dictionary<EVREye, GameObject[]>() {
            { EVREye.Eye_Left, new GameObject[(int)Types.KspCamera.KSP_CAMERA_ENUM_MAX] },
            { EVREye.Eye_Right, new GameObject[(int)Types.KspCamera.KSP_CAMERA_ENUM_MAX] },
        };
        private Types.KspCamera[] currentVrCameras;
        private Dictionary<EVREye, GameObject> vrCameraRigEye = new Dictionary<EVREye, GameObject>();
        private List<Camera> disabledKspCameras = new List<Camera>();
        private Stopwatch sw = new Stopwatch();
        #endregion


        void OnEnable() {
            Events.ManipulatorLeftUpdated.Listen(OnManipulatorLeftUpdated);
            Events.ManipulatorRightUpdated.Listen(OnManipulatorRightUpdated);
        }

        void OnDisable() {
            Events.ManipulatorLeftUpdated.Remove(OnManipulatorLeftUpdated);
            Events.ManipulatorRightUpdated.Remove(OnManipulatorRightUpdated);
        }

        private GameObject CreateVrCameraGameObject(EVREye eye, Types.KspCamera cameraType) {
            string cameraName = "KVR_" + ((eye == EVREye.Eye_Left) ? "CameraEyeL" : "CameraEyeR") + "_" + KSP_CAMERA_NAMES[cameraType];
            GameObject cameraObject = new GameObject(cameraName);
            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.clearFlags = (cameraType == Types.KspCamera.GALAXY) ? CameraClearFlags.Color : CameraClearFlags.Depth;
            cameraComponent.backgroundColor = (cameraType == Types.KspCamera.INTERNAL_CAMERA) ? new Color(0.192f, 0.302f, 0.475f, 0.02f) : new Color(0f, 0f, 0f, 0f);
            cameraComponent.cullingMask = GetCameraCullingMask(cameraType);
            cameraComponent.orthographic = false;
            cameraComponent.fieldOfView = 60f;
            cameraComponent.nearClipPlane = GetCameraNearClipPlane(cameraType);
            cameraComponent.farClipPlane = GetCameraFarClipPlane(cameraType);
            cameraComponent.rect = new Rect(0f, 0f, 1f, 1f);
            cameraComponent.depth = GetCameraDepth(cameraType);
            cameraComponent.renderingPath = RenderingPath.UsePlayerSettings;
            cameraComponent.useOcclusionCulling = true;
            cameraComponent.allowHDR = false;
            cameraComponent.allowMSAA = true;
            cameraComponent.depthTextureMode = DepthTextureMode.None;
            cameraComponent.enabled = false; // disable so we can call Render() directly

            return cameraObject;
        }

        /// <summary>
        /// Sets the render texture to the VR cameras for the given eye, and the
        /// projection matrix for those cameras.
        /// </summary>
        /// <param name="eye">The eye to set cameras for</param>
        /// <param name="targetTexture">The RenderTexture to be targetted for rendering</param>
        public void SetVrCameraParameters(EVREye eye, RenderTexture targetTexture) {
            if (eye == EVREye.Eye_Left) {
                HmdEyeRenderTextureL = targetTexture;
            } else {
                HmdEyeRenderTextureR = targetTexture;
            }
            for (int i = 0; i < (int)Types.KspCamera.KSP_CAMERA_ENUM_MAX; i++) {
                Camera cameraComponent = vrEyeCameras[eye][i].GetComponent<Camera>();
                cameraComponent.targetTexture = targetTexture;

                // set the projection matrix
                HmdMatrix44_t projectionMatrix = OpenVR.System.GetProjectionMatrix(eye, cameraComponent.nearClipPlane, cameraComponent.farClipPlane);
                cameraComponent.projectionMatrix = MathUtils.Matrix4x4_OpenVr2UnityFormat(ref projectionMatrix);
            }
        }

        private int GetCameraCullingMask(Types.KspCamera cameraType) {
            int cullingMask = 0;
            switch (cameraType) {
                case Types.KspCamera.INTERNAL_CAMERA:
                    cullingMask = (1 << 16) | (1 << 20);
                    break;

                case Types.KspCamera.CAMERA_00:
                case Types.KspCamera.CAMERA_01:
                    cullingMask = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 15) | (1 << 17) | (1 << 19) | (1 << 23);
                    break;

                case Types.KspCamera.SCALED_SPACE:
                    cullingMask = (1 << 9) | (1 << 10);
                    break;

                case Types.KspCamera.GALAXY:
                    cullingMask = (1 << 18);
                    break;

                case Types.KspCamera.SCENERY:
                    cullingMask = (1 << 15);
                    break;

                case Types.KspCamera.MAIN:
                    cullingMask = (1 << 0) | (1 << 1) | (1 << 16);
                    break;

                case Types.KspCamera.MARKER:
                    cullingMask = (1 << 2) | (1 << 11);
                    break;

                    default:
                        throw new ArgumentException("Invalid KspCamera type", "cameraType");
            }

            return cullingMask;
        }

        private float GetCameraNearClipPlane(Types.KspCamera cameraType) {
            float clipPlane = 0f;
            switch (cameraType) {
                case Types.KspCamera.INTERNAL_CAMERA:
                    clipPlane = 0.01f;
                    break;

                case Types.KspCamera.CAMERA_00:
                    clipPlane = 0.1f;
                    break;

                case Types.KspCamera.CAMERA_01:
                    clipPlane = 397f;
                    break;

                case Types.KspCamera.SCALED_SPACE:
                    clipPlane = 1f;
                    break;

                case Types.KspCamera.GALAXY:
                    clipPlane = 0.1f;
                    break;

                case Types.KspCamera.SCENERY:
                case Types.KspCamera.MAIN:
                    clipPlane = 0.5f;
                    break;

                case Types.KspCamera.MARKER:
                    clipPlane = 0.3f;
                    break;

                default:
                    throw new ArgumentException("Invalid KspCamera type", "cameraType");
            }

            return clipPlane;
        }

        private float GetCameraFarClipPlane(Types.KspCamera cameraType) {
            float clipPlane = 0f;
            switch (cameraType) {
                case Types.KspCamera.INTERNAL_CAMERA:
                    clipPlane = 50f;
                    break;

                case Types.KspCamera.CAMERA_00:
                    clipPlane = 400f;
                    break;

                case Types.KspCamera.CAMERA_01:
                    clipPlane = 750000f;
                    break;

                case Types.KspCamera.SCALED_SPACE:
                    clipPlane = 30000000f;
                    break;

                case Types.KspCamera.GALAXY:
                    clipPlane = 20f;
                    break;

                case Types.KspCamera.SCENERY:
                case Types.KspCamera.MAIN:
                    clipPlane = 1200f;
                    break;

                case Types.KspCamera.MARKER:
                    clipPlane = 1000f;
                    break;

                default:
                    throw new ArgumentException("Invalid KspCamera type", "cameraType");
            }

            return clipPlane;
        }

        private float GetCameraDepth(Types.KspCamera cameraType) {
            float depth = 0f;
            switch (cameraType) {
                case Types.KspCamera.INTERNAL_CAMERA:
                    depth = 3f;
                    break;

                case Types.KspCamera.CAMERA_00:
                    depth = 0f;
                    break;

                case Types.KspCamera.MAIN:
                case Types.KspCamera.CAMERA_01:
                    depth = -1f;
                    break;

                case Types.KspCamera.SCALED_SPACE:
                    depth = -3f;
                    break;

                case Types.KspCamera.GALAXY:
                    depth = -4f;
                    break;

                case Types.KspCamera.SCENERY:
                    depth = -2f;
                    break;

                case Types.KspCamera.MARKER:
                    depth = 1f;
                    break;

                default:
                    throw new ArgumentException("Invalid KspCamera type", "cameraType");
            }

            return depth;
        }

        /// <summary>
        /// Renders the VR camera rig onto the target render texture.
        /// </summary>
        public void RenderVrCameras(EVREye eye) {
            if (currentVrCameras != null) {
                for (int i = 0; i < currentVrCameras.Length; i++) {
                    Types.KspCamera cameraType = currentVrCameras[i];
                    int cameraIndex = (int)cameraType;
                    Camera cameraComponent = vrEyeCameras[eye][cameraIndex].GetComponent<Camera>();
                    cameraComponent.Render();
                }
            }
        }

        /// <summary>
        /// Set up the list of cameras to render for this scene and the initial position
        /// corresponding to the origin in the real world device coordinate system.
        /// </summary>
        public void SetupScene() {
            switch (HighLogic.LoadedScene) {
                case GameScenes.FLIGHT:
                    if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) {
                        SetupFlightIvaScene();
                    } else if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.isEVA) {
                        SetupFlightEvaScene();
                    }
                    break;

                case GameScenes.EDITOR:
                    SetupEditorScene();
                    break;

                default:
                    throw new Exception("Cannot setup VR scene, current scene \"" +
                        HighLogic.LoadedScene + "\" is invalid.");
            }

            CurrentPosition = InitialPosition;
            CurrentRotation = InitialRotation;
        }

        private void SetupFlightIvaScene() {
            // use seated mode during IVA flight
            TrackingSpace = ETrackingUniverseOrigin.TrackingUniverseSeated;

            // render KerbalVR objects on the InternalSpace layer
            RenderLayer = 20;

            // generate list of cameras to render
            PrepareCameras(FLIGHT_SCENE_IVA_CAMERAS);

            // set inital scene position
            InitialPosition = InternalCamera.Instance.transform.position;

            // set rotation to always point forward inside the cockpit
            // NOTE: actually this code doesn't work for certain capsules
            // with different internal origin orientations
            /*InitialRotation = Quaternion.LookRotation(
                InternalSpace.Instance.transform.rotation * Vector3.up,
                InternalSpace.Instance.transform.rotation * Vector3.back);*/
            InitialRotation = InternalCamera.Instance.transform.rotation;
        }

        private void SetupFlightEvaScene() {
            // use seated mode during EVA
            TrackingSpace = ETrackingUniverseOrigin.TrackingUniverseSeated;

            // render KerbalVR objects on the InternalSpace layer
            RenderLayer = 20;

            // generate list of cameras to render
            PrepareCameras(FLIGHT_SCENE_EVA_CAMERAS);

            // set inital scene position
            InitialPosition = FlightGlobals.ActiveVessel.transform.position;

            // set rotation to always point forward inside the cockpit
            // NOTE: actually this code doesn't work for certain capsules
            // with different internal origin orientations
            /*InitialRotation = Quaternion.LookRotation(
                InternalSpace.Instance.transform.rotation * Vector3.up,
                InternalSpace.Instance.transform.rotation * Vector3.back);*/
            InitialRotation = FlightGlobals.ActiveVessel.transform.rotation;
        }

        private void SetupEditorScene() {
            // use room-scale in editor
            TrackingSpace = ETrackingUniverseOrigin.TrackingUniverseStanding;

            // render KerbalVR objects on the default layer
            RenderLayer = 0;

            // generate list of cameras to render
            PrepareCameras(EDITOR_SCENE_CAMERAS);

            // set inital scene position

            //Vector3 forwardDir = EditorCamera.Instance.transform.rotation * Vector3.forward;
            //forwardDir.y = 0f; // make the camera point straight forward
            //Vector3 startingPos = EditorCamera.Instance.transform.position;
            //startingPos.y = 0f; // start at ground level

            Vector3 startingPos = new Vector3(0f, 0f, -5f);

            InitialPosition = startingPos;
            InitialRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        /// <summary>
        /// Updates the game cameras to the correct position, according to the given HMD eye.
        /// </summary>
        /// <param name="eye">The HMD eye</param>
        /// <param name="hmdTransform">The HMD pose</param>
        /// <param name="hmdEyeTransform">The eye pose relative to the HMD pose</param>
        public void UpdateScene(
            EVREye eye,
            SteamVR_Utils.RigidTransform hmdTransform,
            SteamVR_Utils.RigidTransform hmdEyeTransform) {

            switch (HighLogic.LoadedScene) {
                case GameScenes.FLIGHT:
                    if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) {
                        UpdateFlightIvaScene(eye, hmdTransform, hmdEyeTransform);
                    } else if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.isEVA) {
                        UpdateFlightEvaScene(eye, hmdTransform, hmdEyeTransform);
                    }
                    break;

                case GameScenes.EDITOR:
                    UpdateEditorScene(eye, hmdTransform, hmdEyeTransform);
                    break;

                default:
                    throw new Exception("Cannot setup VR scene, current scene \"" +
                        HighLogic.LoadedScene + "\" is invalid.");
            }

            HmdPosition = CurrentPosition + CurrentRotation * hmdTransform.pos;
            HmdRotation = CurrentRotation * hmdTransform.rot;
        }

        private void UpdateFlightIvaScene(
            EVREye eye,
            SteamVR_Utils.RigidTransform hmdTransform,
            SteamVR_Utils.RigidTransform hmdEyeTransform) {

            // in flight, don't allow movement of the origin point
            CurrentPosition = InitialPosition;
            CurrentRotation = InitialRotation;

            // move the rig to position the VR cameras
            vrCameraRigEye[eye].transform.localPosition = hmdEyeTransform.pos;
            vrCameraRigEye[eye].transform.localRotation = hmdEyeTransform.rot;
            VrCameraRig.transform.position = DevicePoseToWorld(hmdTransform.pos);
            VrCameraRig.transform.rotation = DevicePoseToWorld(hmdTransform.rot);

            // world-space cameras need to be positioned separately from the VR camera rig
            for (int i = 0; i < WORLD_SPACE_CAMERAS.Length; i++) {
                vrEyeCameras[eye][(int)WORLD_SPACE_CAMERAS[i]].transform.position = InternalSpace.InternalToWorld(vrCameraRigEye[eye].transform.position);
                vrEyeCameras[eye][(int)WORLD_SPACE_CAMERAS[i]].transform.rotation = InternalSpace.InternalToWorld(vrCameraRigEye[eye].transform.rotation);
            }

            // get position of your eyeball
            Vector3 positionToEye = hmdTransform.pos + hmdTransform.rot * hmdEyeTransform.pos;

            // translate device space to Unity space, with world scaling
            Vector3 updatedPosition = DevicePoseToWorld(positionToEye);
            Quaternion updatedRotation = DevicePoseToWorld(hmdTransform.rot);

            // store the eyeball position
            HmdEyePosition[(int)eye] = updatedPosition;
            HmdEyeRotation[(int)eye] = updatedRotation;
        }

        private void UpdateFlightEvaScene(
            EVREye eye,
            SteamVR_Utils.RigidTransform hmdTransform,
            SteamVR_Utils.RigidTransform hmdEyeTransform) {

            // in flight, don't allow movement of the origin point
            CurrentPosition = InitialPosition;
            CurrentRotation = InitialRotation;

            // get position of your eyeball
            Vector3 positionToHmd = hmdTransform.pos;
            Vector3 positionToEye = hmdTransform.pos + hmdTransform.rot * hmdEyeTransform.pos;

            // translate device space to Unity space, with world scaling
            Vector3 updatedPosition = DevicePoseToWorld(positionToEye);
            Quaternion updatedRotation = DevicePoseToWorld(hmdTransform.rot);

            // in flight, update the flight cameras
            // FlightCamera.fetch.transform.position = updatedPosition;
            // FlightCamera.fetch.transform.rotation = updatedRotation;

            // ScaledCamera.Instance.transform.position = updatedPosition;
            // ScaledCamera.Instance.transform.rotation = updatedRotation;

            // store the eyeball position
            HmdEyePosition[(int)eye] = updatedPosition;
            HmdEyeRotation[(int)eye] = updatedRotation;
        }

        private void UpdateEditorScene(
            EVREye eye,
            SteamVR_Utils.RigidTransform hmdTransform,
            SteamVR_Utils.RigidTransform hmdEyeTransform) {

            // move the rig to position the VR cameras
            vrCameraRigEye[eye].transform.localPosition = hmdEyeTransform.pos;
            vrCameraRigEye[eye].transform.localRotation = hmdEyeTransform.rot;
            VrCameraRig.transform.position = DevicePoseToWorld(hmdTransform.pos);
            VrCameraRig.transform.rotation = DevicePoseToWorld(hmdTransform.rot);

            // get position of your eyeball
            Vector3 positionToEye = hmdTransform.pos + hmdTransform.rot * hmdEyeTransform.pos;

            // store the eyeball position
            HmdEyePosition[(int)eye] = DevicePoseToWorld(positionToEye);
            HmdEyeRotation[(int)eye] = DevicePoseToWorld(hmdTransform.rot);
        }

        /// <summary>
        /// Resets game cameras back to their original settings
        /// </summary>
        public void CloseScene() {
            // re-enable the KSP cameras we disabled previously
            foreach (Camera kspCam in disabledKspCameras) {
                kspCam.enabled = true;
            }
        }

        /// <summary>
        /// Populates the list of cameras according to the cameras that should be used for
        /// the current game scene.
        /// </summary>
        /// <param name="cameraNames">An array of camera names to use for this VR scene.</param>
        private void PrepareCameras(Types.KspCamera[] cameraList) {
            // set the list of VR cameras to render
            currentVrCameras = cameraList;

            // search for the KSP game cameras to disable
            disabledKspCameras.Clear();
            for (int i = 0; i < currentVrCameras.Length; i++) {
                string cameraName = KSP_CAMERA_NAMES[currentVrCameras[i]];
                Camera foundCamera = Array.Find(Camera.allCameras, cam => cam.name.Equals(cameraName));
                if (foundCamera == null) {
                    Utils.LogError("PopulateCameraList: Could not find camera \"" + cameraName + "\" in the scene!");
                } else {
                    // disable the KSP game camera from rendering, since we will be calling our own
                    // VR cameras to render onto the VR render texture.
                    foundCamera.enabled = false;
                    disabledKspCameras.Add(foundCamera);
                }
            }
        }

        public bool SceneAllowsVR() {
            bool allowed;
            switch (HighLogic.LoadedScene) {
                case GameScenes.FLIGHT:
                    allowed = ((CameraManager.Instance != null) && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)) ||
                        (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.isEVA);
                    break;

                case GameScenes.EDITOR:
                    allowed = true;
                    break;

                default:
                    allowed = false;
                    break;
            }
            return allowed;
        }

        /// <summary>
        /// Convert a device position to Unity world coordinates for this scene.
        /// </summary>
        /// <param name="devicePosition">Device position in the device space coordinate system.</param>
        /// <returns>Unity world position corresponding to the device position.</returns>
        public Vector3 DevicePoseToWorld(Vector3 devicePosition) {
            return CurrentPosition + CurrentRotation *
                (devicePosition * inverseWorldScale[HighLogic.LoadedScene]);
        }

        /// <summary>
        /// Convert a device rotation to Unity world coordinates for this scene.
        /// </summary>
        /// <param name="deviceRotation">Device rotation in the device space coordinate system.</param>
        /// <returns>Unity world rotation corresponding to the device rotation.</returns>
        public Quaternion DevicePoseToWorld(Quaternion deviceRotation) {
            return CurrentRotation * deviceRotation;
        }

        public void OnManipulatorLeftUpdated(SteamVR_Controller.Device state) {
            // left touchpad
            if (state.GetPress(EVRButtonId.k_EButton_SteamVR_Touchpad)) {
                Vector2 touchAxis = state.GetAxis(EVRButtonId.k_EButton_SteamVR_Touchpad);

                Vector3 upDisplacement = Vector3.up *
                    (editorMovementSpeed * inverseWorldScale[HighLogic.LoadedScene] * touchAxis.y) * Time.deltaTime;

                Vector3 newPosition = CurrentPosition + upDisplacement;
                if (newPosition.y < 0f) newPosition.y = 0f;

                CurrentPosition = newPosition;
            }

            // left menu button
            if (state.GetPressDown(EVRButtonId.k_EButton_ApplicationMenu)) {
                Core.ResetInitialHmdPosition();
            }

            // simulate mouse touch events with the trigger
            if (state.GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger)) {
                foreach (var obj in DeviceManager.Instance.ManipulatorLeft.FingertipCollidedGameObjects) {
                    obj.SendMessage("OnMouseDown");
                }
            }

            if (state.GetPressUp(EVRButtonId.k_EButton_SteamVR_Trigger)) {
                foreach (var obj in DeviceManager.Instance.ManipulatorLeft.FingertipCollidedGameObjects) {
                    obj.SendMessage("OnMouseUp");
                }
            }
        }

        public void OnManipulatorRightUpdated(SteamVR_Controller.Device state) {
            // right touchpad
            if (state.GetPress(EVRButtonId.k_EButton_SteamVR_Touchpad)) {
                Vector2 touchAxis = state.GetAxis(EVRButtonId.k_EButton_SteamVR_Touchpad);

                Vector3 fwdDirection = HmdRotation * Vector3.forward;
                fwdDirection.y = 0f; // allow only planar movement
                Vector3 fwdDisplacement = fwdDirection.normalized *
                    (editorMovementSpeed * inverseWorldScale[HighLogic.LoadedScene] * touchAxis.y) * Time.deltaTime;

                Vector3 rightDirection = HmdRotation * Vector3.right;
                rightDirection.y = 0f; // allow only planar movement
                Vector3 rightDisplacement = rightDirection.normalized *
                    (editorMovementSpeed * inverseWorldScale[HighLogic.LoadedScene] * touchAxis.x) * Time.deltaTime;

                CurrentPosition += fwdDisplacement + rightDisplacement;
            }

            // right menu button
            if (state.GetPressDown(EVRButtonId.k_EButton_ApplicationMenu)) {
                Core.ResetInitialHmdPosition();
            }

            // simulate mouse touch events with the trigger
            if (state.GetPressDown(EVRButtonId.k_EButton_SteamVR_Trigger)) {
                foreach (var obj in DeviceManager.Instance.ManipulatorRight.FingertipCollidedGameObjects) {
                    obj.SendMessage("OnMouseDown");
                }
            }

            if (state.GetPressUp(EVRButtonId.k_EButton_SteamVR_Trigger)) {
                foreach (var obj in DeviceManager.Instance.ManipulatorRight.FingertipCollidedGameObjects) {
                    obj.SendMessage("OnMouseUp");
                }
            }
        }
    } // class Scene
} // namespace KerbalVR

using FFXIVClientStructs.FFXIV.Client.Game;
using System.Numerics;

namespace FFStreamViewer.Livestream;

/// <summary> 
/// /// This class is used to handle the game objects for the FFStreamViewer plugin.
/// </summary>
public class MediaCameraObject {
    private unsafe Camera* _camera; // define the camera object this is based on
    public string Name => "Camera"; // Name it
    private Vector3 _position;      // define the position of the object 

    /// <summary> Constructor for the MediaCameraObject class. </summary>
    public unsafe MediaCameraObject() {
        this._camera = null; // we want to first set it to null so that we know if we have assigned one yet or not
    }

    public unsafe void SetCameraObject(Camera* camera) {
        this._camera = camera;
    }


    /// <summary> Method to set the position of the MediaCameraObject. </summary>
    unsafe public Vector3 Position {
        get {
            return _camera->CameraBase.SceneCamera.Object.Position;
        }
    }

    /// <summary> Method to set the name and position of the MediaCameraObject. </summary>
    unsafe public float Rotation {
        get {
            return _camera->CameraBase.SceneCamera.Object.Rotation.EulerAngles.Y;
        }
    }

    /// <summary> Override for the GameObject's name attribute. </summary>
    unsafe public Vector3 Forward {
        get {
            var cameraViewMatrix = _camera->CameraBase.SceneCamera.ViewMatrix;
            return new Vector3(cameraViewMatrix.M13, cameraViewMatrix.M23, cameraViewMatrix.M33);
        }
    }

    /// <summary> Override for the GameObject's name attribute. </summary>
    unsafe public Vector3 Top {
        get {
            return _camera->CameraBase.SceneCamera.Vector_1; ;
        }
    }

    /// <summary> Override for the GameObject's name attribute. </summary>
    public string FocusedPlayerObject {
        get {
            return "";
        }
    }
}
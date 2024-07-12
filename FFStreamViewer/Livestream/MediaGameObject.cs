using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;

namespace FFStreamViewer.Livestream;

/// <summary> 
/// /// This class is used to handle the game objects for the FFStreamViewer plugin.
/// </summary>
public class MediaGameObject {
    private IGameObject _gameObject; // define the game object this is based on
    private string _name;           // define the name of the object
    private Vector3 _position;      // define the position of the object 

    /// <summary> Constructor for the MediaGameObject class. </summary>
    public MediaGameObject() {
        _gameObject = null;
        //_gameObject = gameObject;
        _name = "";
        _position = new Vector3();
    }

    public MediaGameObject(IGameObject gameObject) {
        _gameObject = gameObject;
        //_gameObject = gameObject;
        _name = "";
        _position = new Vector3();
    }

    public void SetGameObject(IGameObject gameObject) {
        _gameObject = gameObject;
    }

    /// <summary> Method to set the name of the MediaGameObject. </summary>
    public void SetName(string name) { _name = name; }

    /// <summary> Method to set the position of the MediaGameObject. </summary>
    public void SetPosition(Vector3 position) { _position = position; }

    /// <summary> Method to set the name and position of the MediaGameObject. </summary>
    public void SetNameAndPosition(string name, Vector3 position) {
        _name = name;
        _position = position;
    }

    /// <summary> Override for the IGameObject's name attribute. </summary>
    public string Name {
        get {
            try {
                return _gameObject != null ? (_gameObject.Name != null ? _gameObject.Name.TextValue : _name) : _name;
            } catch {
                return _name;
            }
        }
    }

    /// <summary> Override for the IGameObject's position attribute. </summary>
    public Vector3 Position {
        get {
            try {
                return (_gameObject != null ? _gameObject.Position : _position);
            } catch {
                return _position;
            }
        }
    }

    /// <summary> Override for the IGameObject's rotation attribute. </summary>
    public float Rotation {
        get {
            try {
                return _gameObject != null ? _gameObject.Rotation : 0;
            } catch {
                return 0;
            }
        }
    }

    /// <summary> Override for the IGameObject's forward attribute. </summary>
    public Vector3 Forward {
        get {
            float rotation = _gameObject != null ? _gameObject.Rotation : 0;
            return new Vector3((float)Math.Cos(rotation), 0, (float)Math.Sin(rotation));
        }
    }

    /// <summary> Override for the IGameObject's top attribute. </summary>
    public Vector3 Top {
        get {
            return new Vector3(0, 1, 0);
        }
    }
    
    /// <summary> Override for the IGameObject's focused player object attribute. </summary>
    public string FocusedPlayerObject {
        get {
            if (_gameObject != null) {
                try {
                    return _gameObject.TargetObject != null ?
                        (_gameObject.TargetObject.ObjectKind == ObjectKind.Player ? _gameObject.TargetObject.Name.TextValue : "")
                        : "";
                } catch {
                    return "";
                }
            } else {
                return "";
            }
        }
    }

}

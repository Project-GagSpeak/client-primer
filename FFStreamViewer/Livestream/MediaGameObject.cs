using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Numerics;

namespace FFStreamViewer.Livestream;

/// <summary>
/// This class is used to handle the game objects for the FFStreamViewer plugin.
/// </summary>
public class MediaGameObject : IGameObject {
    private GameObject _gameObject;             // define the game object this is based on
    private string _name = "";                  // define the name of the object
    private Vector3 _position = new Vector3();  // define the position of the object 

    /// <summary>
    /// Override for the GameObject's name attribute.
    /// </summary>
    string IGameObject.Name {
        get {
            try {
                return _gameObject != null ? (_gameObject.Name != null ? _gameObject.Name.TextValue : _name) : _name;
            } catch {
                return _name;
            }
        }
    }

    /// <summary>
    /// Override for the GameObject's position attribute.
    /// </summary>
    Vector3 IGameObject.Position {
        get {
            try {
                return (_gameObject != null ? _gameObject.Position : _position);
            } catch {
                return _position;
            }
        }
    }

    /// <summary>
    /// Override for the GameObject's rotation attribute.
    /// </summary>
    float IGameObject.Rotation {
        get {
            try {
                return _gameObject != null ? _gameObject.Rotation : 0;
            } catch {
                return 0;
            }
        }
    }

    /// <summary>
    /// Override for the GameObject's focused player object attribute.
    /// </summary>
    string IGameObject.FocusedPlayerObject {
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

    /// <summary>
    /// Override for the GameObject's forward attribute.
    /// </summary>
    Vector3 IGameObject.Forward {
        get {
            float rotation = _gameObject != null ? _gameObject.Rotation : 0;
            return new Vector3((float)Math.Cos(rotation), 0, (float)Math.Sin(rotation));
        }
    }

    /// <summary>
    /// Override for the GameObject's top attribute.
    /// </summary>
    public Vector3 Top {
        get {
            return new Vector3(0, 1, 0);
        }
    }

    /// <summary> Constructor for the MediaGameObject class. </summary>
    public MediaGameObject(GameObject gameObject) {
        _gameObject = gameObject;
    }

    /// <summary> Augmented Constructor for the mediaGame object class. [currently missing the base game object and leaving it undefined]
    /// <list type="bullet">
    /// <item><c>name</c><param name="name"> - The name of the object.</param></item>
    /// <item><c>position</c><param name="position"> - The position of the object.</param></item></list>
    /// </summary>
    public MediaGameObject(string name, Vector3 position) {
        _name = name;
        _position = position;
    }

    /// <summary> Augmented Constructor for the mediaGame object class.
    /// <list type="bullet">
    /// <item><c>gameObject</c><param name="gameObject"> - The game object.</param></item>
    /// <item><c>name</c><param name="name"> - The name of the object.</param></item>
    /// <item><c>position</c><param name="position"> - The position of the object.</param></item></list>
    /// </summary>
    public MediaGameObject(GameObject gameObject, string name, Vector3 position) {
        _gameObject = gameObject;
        _name = name;
        _position = position;
    }
}

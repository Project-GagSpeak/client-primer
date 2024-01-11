using System.Numerics;

namespace FFStreamViewer {
    public interface IGameObject {
        public string   Name { get; }                   // name of the object
        public Vector3  Position { get; }               // where is the object
        public float    Rotation { get; }               // what is the rotation of the object
        public Vector3  Forward { get; }                // what is the forward vector of the object
        public Vector3  Top { get; }                    // what is the top vector of the object
        public string   FocusedPlayerObject { get; }    // what is the focused player object
    }
}
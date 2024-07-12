using System;

namespace FFStreamViewer.Events
{
    public class MediaError {
        public Exception Exception { get; internal set; }
        
        public MediaError() { }
    }
}

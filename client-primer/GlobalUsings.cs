/*
 * A Simple file for storing global using variables 
 * so I don't need to be redundant all the time in headers. 
 */
global using Microsoft.Extensions.Logging;
global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Collections;
global using System.Diagnostics;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;

global using MoodlesStatusInfo = (
    System.Guid GUID,
    int IconID,
    string Title,
    string Description,
    GagspeakAPI.Data.IPC.StatusType Type,
    string Applier,
    bool Dispelable,
    int Stacks,
    bool Persistent,
    int Days,
    int Hours,
    int Minutes,
    int Seconds,
    bool NoExpire,
    bool AsPermanent
);

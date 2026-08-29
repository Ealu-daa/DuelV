using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterId
{
    None = 0,

    // Duelist: 1000番台
    Hollow = 1001,
    Storm = 1002,
    Blaze = 1003,
    Reaper = 1004,

    // Guardian: 2000番台
    Fort = 2001,
    Aegis = 2002,
    Double = 2003,

    // Controller: 3000番台
    Phantom = 3001,
    Lapse = 3002,
    Chain = 3003,
    Chronicle = 3004,
    Aine = 3005,

    // Support: 4000番台
    Lumina = 4001,
    Grace = 4002,
    Morphe = 4003,
}

public enum Role
{
    None = 0,
    Duelist = 1,
    Guardian = 2,
    Controller = 3,
    Support = 4,
}
﻿namespace GrowattMonitor.Models;

// These are not produced by Growatt ShineWiFi I have
//ANNOUNCE50 = 0x5003,
//ANNOUNCE51 = 0x5103,
//DATA50 = 0x5004,
//DATA51 = 0x5104,
//CONFIG51 = 0x5118,
//IDENTIFY51 = 0x5119,
//CONFACK50 = 0x5029,
//CONFACK51 = 0x5129,

public enum MessageType
{
    ANNOUNCE = 0x0103,
    CURRDATA = 0x0104,
    HISTDATA = 0x0150,
    PING = 0x0116,
    CONFIG = 0x0118,
    IDENTIFY = 0x0119,
    REBOOT = 0x0120,
    CONFACK = 0x3129
}

namespace GrowattMonitorShared
{
    public enum MonitorState
    {
        WAITING = 0,
        OPEN = 1,
        PING_SENT = 2,
        PING_RCVD = 3,
        IDENTIFY_SENT = 4,
        IDENTIFY_RCVD = 5,
        ANNOUNCE_SENT = 6,
        ANNOUNCE_RCVD = 7,
        CURRDATA_SENT = 8,
        CURRDATA_RCVD = 7,
        HISTDATA_SENT = 9,
        HISTDATA_RCVD = 10,
        CONFIG_SENT = 11,
        CONFIG_RCVD = 12
    }
}

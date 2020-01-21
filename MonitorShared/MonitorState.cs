namespace GrowattMonitorShared
{
    public enum MonitorState
    {
        WAITING = 0,
        OPEN = 1,
        IDENTIFY_SENT = 2,
        TIME_SET = 3,
        IDENTIFY_RCVD = 4,
        ANNOUNCE_RCVD = 5,
        ANNOUNCE_SENT = 6,
        DATA_RCVD = 7

    }
}

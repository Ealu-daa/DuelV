using System;

[Serializable]
public class CatalystInstance
{
    public CatalystData data;
    public bool isUsed;
    public int disabledUntilHalfTurn = -1; // Lapseのハンドピーク用: この値以下のhtまでは使用不可

    public CatalystInstance(CatalystData data)
    {
        this.data = data;
        this.isUsed = false;
    }

    public CatalystId Id => data.id;

    public void MarkUsed()
    {
        isUsed = true;
    }

    public bool IsDisabled(int currentHalfTurn)
    {
        return currentHalfTurn <= disabledUntilHalfTurn;
    }
}
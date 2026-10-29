using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using Mode = MovementModeSelector.MovementMode;

// P##Coins.csv, written as each coin is collected. Every cell has a fixed meaning so files line up:
// column 1 is the passenger role and column 2 the conductor role, whichever came first.
// Row 1 participant, row 2 role, rows 3-7 Continuous Turn, 8-12 Joystick, 13-17 Tilt,
// with column 3 naming the turn type of each of those rows.
// Each turn type block holds seconds since that area started, one row per coin in name order.
public static class StudyLog
{
    private static readonly Mode[] TurnBlocks = { Mode.ContinuousRotation, Mode.UseJoystick, Mode.UseRotation };
    private static readonly string[] CoinNames = { "coin", "coin (2)", "coin (3)", "coin (4)", "coin (5)" };
    private const int HeaderRows = 2;
    private const int PassengerColumn = 0;
    private const int ConductorColumn = 1;
    private const int TurnTypeColumn = 2;

    public static string FilePath => ParticipantFile("Coins");

    // P##<kind>.csv, e.g. P07Frames.csv.
    public static string ParticipantFile(string kind) => Path.Combine(Application.persistentDataPath,
        "P" + MovementSessionSettings.ParticipantId.ToString("00", CultureInfo.InvariantCulture) + kind + ".csv");

    // 0 Continuous Turn, 1 Joystick, 2 Tilt: the order both CSVs use.
    public static int TurnTypeIndex(Mode mode) => Array.IndexOf(TurnBlocks, mode);

    //Starts a fresh file for the current participant.
    public static void Begin() => Write(NewCells());

    public static void RecordCoin(string coinName, float secondsIntoArea)
    {
        int coin = Array.IndexOf(CoinNames, coinName);
        if (coin < 0)
        {
            Debug.LogWarning("StudyLog: no row for coin '" + coinName + "'.");
            return;
        }

        StudyStep step = StudyFlow.Current;
        int row = HeaderRows + TurnTypeIndex(step.Mode) * CoinNames.Length + coin;
        string[][] cells = NewCells();
        try
        {
            if (File.Exists(FilePath))
            {
                string[] lines = File.ReadAllLines(FilePath);
                for (int r = HeaderRows; r < cells.Length && r < lines.Length; r++)
                {
                    // Only the times carry over; labels always come from NewCells.
                    string[] existing = lines[r].Split(',');
                    for (int c = 0; c < TurnTypeColumn && c < existing.Length; c++)
                        cells[r][c] = existing[c];
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        cells[row][step.AsConductor ? ConductorColumn : PassengerColumn] =
            secondsIntoArea.ToString("F3", CultureInfo.InvariantCulture);
        Write(cells);
    }

    private static string[][] NewCells()
    {
        var cells = new string[HeaderRows + TurnBlocks.Length * CoinNames.Length][];
        for (int r = 0; r < cells.Length; r++) cells[r] = new[] { "", "", "" };
        cells[0][PassengerColumn] = MovementSessionSettings.ParticipantId.ToString(CultureInfo.InvariantCulture);
        cells[1][PassengerColumn] = "as passenger";
        cells[1][ConductorColumn] = "as conductor";
        for (int r = HeaderRows; r < cells.Length; r++)
            cells[r][TurnTypeColumn] = MainMenuController.TurnLabel(TurnBlocks[(r - HeaderRows) / CoinNames.Length]);
        return cells;
    }

    private static void Write(string[][] cells)
    {
        var lines = new string[cells.Length];
        for (int r = 0; r < cells.Length; r++) lines[r] = string.Join(",", cells[r]);
        try
        {
            File.WriteAllLines(FilePath, lines);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}

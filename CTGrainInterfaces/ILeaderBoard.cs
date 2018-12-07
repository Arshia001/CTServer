using Orleans;
using Orleans.Concurrency;
using OrleansBondUtils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public class LeaderBoardEntry
    {
        public Guid Id;
        public ulong Score;
        public ulong Rank;
    }

    [BondSerializationTag("@lb")]
    public interface ILeaderBoard : IGrainWithStringKey
    {
        Task Set(Guid Id, ulong Score);
        Task<ulong> AddDelta(Guid Id, ulong DeltaScore);
        Task<ulong> GetRank(Guid Id);
        Task<Immutable<List<LeaderBoardEntry>>> GetTopScores(uint Count);
        Task<Immutable<List<LeaderBoardEntry>>> GetScoresAround(Guid Id, uint CountInEachDirection);
        Task<Immutable<Tuple<ulong, List<LeaderBoardEntry>>>> GetScoresForDisplay(Guid UserID);
        Task Initialize(); // To init the leaderboard and load data into memory at startup time
        Task CheckEndMonthHighScores();
    }

    public static class LeaderBoardUtil
    {
        public const string LifetimeId = "g";
        static readonly Calendar Calendar = new PersianCalendar();

        public static string GetIdString(int Year, int Month)
        {
            return Year.ToString() + "-" + Month.ToString();
        }

        public static string GetIdString(DateTime Date)
        {
            return GetIdString(Calendar.GetYear(Date), Calendar.GetMonth(Date));
        }

        public static string GetThisMonthIdString()
        {
            return GetIdString(DateTime.Now);
        }

        public static DateTime GetStartOfMonth(int MonthOffsetFromNow)
        {
            var Date = DateTime.Now;
            var Year = Calendar.GetYear(Date);
            var Month = Calendar.GetMonth(Date);
            return Calendar.AddMonths(Calendar.ToDateTime(Year, Month, 1, 0, 0, 0, 0), MonthOffsetFromNow);
        }

        public static string GetMonthIdString(int MonthOffsetFromNow)
        {
            var Date = GetStartOfMonth(MonthOffsetFromNow);
            var Year = Calendar.GetYear(Date);
            var Month = Calendar.GetMonth(Date);

            return GetIdString(Year, Month);
        }

        public static string GetLastMonthIdString()
        {
            return GetMonthIdString(-1);
        }
    }
}

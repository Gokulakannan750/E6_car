namespace E6CarSpa.Api.Services;

/// <summary>
/// Helpers for reporting in India Standard Time. The database stores timestamps in UTC, but the
/// shop thinks in IST days — so reports/dashboards group and bound by IST, converted to UTC for queries.
/// </summary>
public static class IndianTime
{
	public static readonly TimeZoneInfo Zone = ResolveZone();

	private static TimeZoneInfo ResolveZone()
	{
		foreach (var id in new[] { "India Standard Time", "Asia/Kolkata" })
		{
			try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { /* try next */ }
		}
		return TimeZoneInfo.Utc; // last-resort fallback
	}

	/// <summary>UTC half-open range [start, end) covering the IST days from..to (both inclusive).</summary>
	public static (DateTime startUtc, DateTime endUtc) RangeUtc(DateTime from, DateTime to)
	{
		var startLocal = DateTime.SpecifyKind(from.Date, DateTimeKind.Unspecified);
		var endLocal = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Unspecified);
		return (DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(startLocal, Zone), DateTimeKind.Unspecified),
				DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(endLocal, Zone), DateTimeKind.Unspecified));
	}

	/// <summary>UTC range covering "today" in IST.</summary>
	public static (DateTime startUtc, DateTime endUtc) TodayUtc()
	{
		var todayIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone).Date;
		return RangeUtc(todayIst, todayIst);
	}

	/// <summary>The IST calendar date for a stored UTC timestamp.</summary>
	public static DateTime ToIstDate(DateTime utc) =>
		TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).Date;
}

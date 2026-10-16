/// An inclusive range of calendar days, plus the quick choices the report screens offer.
class DateRange {
  const DateRange(this.from, this.to);

  final DateTime from;
  final DateTime to;

  int get days => to.difference(from).inDays + 1;

  static DateTime _day(DateTime d) => DateTime(d.year, d.month, d.day);

  factory DateRange.today(DateTime now) => DateRange(_day(now), _day(now));

  /// The last [days] days ending today (so 7 = today and the six days before).
  factory DateRange.lastDays(DateTime now, int days) =>
      DateRange(_day(now).subtract(Duration(days: days - 1)), _day(now));

  factory DateRange.thisMonth(DateTime now) =>
      DateRange(DateTime(now.year, now.month, 1), _day(now));

  factory DateRange.lastMonth(DateTime now) {
    final first = DateTime(now.year, now.month - 1, 1);
    return DateRange(first, DateTime(now.year, now.month, 0));
  }

  factory DateRange.thisYear(DateTime now) =>
      DateRange(DateTime(now.year, 1, 1), _day(now));

  /// Same length, moved back so it ends the day before this one starts.
  DateRange get previous => DateRange(
    from.subtract(Duration(days: days)),
    from.subtract(const Duration(days: 1)),
  );

  @override
  bool operator ==(Object other) =>
      other is DateRange && other.from == from && other.to == to;

  @override
  int get hashCode => Object.hash(from, to);

  @override
  String toString() => 'DateRange($from..$to)';
}

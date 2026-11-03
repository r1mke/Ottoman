import 'package:flutter/material.dart';
import 'package:table_calendar/table_calendar.dart';

import '../l10n/app_localizations.dart';
import '../theme/ottoman_theme.dart';

/// A month calendar for choosing a day: the chosen day is an amber circle, days that cannot be booked are dimmed
/// with a red dot (and cannot be chosen), and a legend says what the dot means.
///
/// [available] is null while the month is still loading: nothing is dimmed or blocked until it is known.
class AvailabilityCalendar extends StatelessWidget {
  const AvailabilityCalendar({
    super.key,
    required this.selected,
    required this.first,
    required this.last,
    required this.focused,
    required this.available,
    required this.onSelected,
    required this.onMonthChanged,
  });

  final DateTime selected;
  final DateTime first;
  final DateTime last;

  /// Any day of the month being shown.
  final DateTime focused;

  /// The days that can be booked in [focused]'s month, or null while unknown.
  final Set<DateTime>? available;
  final ValueChanged<DateTime> onSelected;
  final ValueChanged<DateTime> onMonthChanged;

  static const _months = {
    'bs': [
      'Januar',
      'Februar',
      'Mart',
      'April',
      'Maj',
      'Juni',
      'Juli',
      'August',
      'Septembar',
      'Oktobar',
      'Novembar',
      'Decembar',
    ],
    'en': [
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ],
  };

  static const _weekdays = {
    'bs': ['Pon', 'Uto', 'Sri', 'Čet', 'Pet', 'Sub', 'Ned'],
    'en': ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
  };

  static DateTime _day(DateTime d) => DateTime(d.year, d.month, d.day);

  bool _bookable(DateTime day) {
    final d = _day(day);
    if (d.isBefore(_day(first)) || d.isAfter(_day(last))) return false;
    return available == null || available!.contains(d);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    final lang = Localizations.localeOf(context).languageCode == 'bs'
        ? 'bs'
        : 'en';
    final months = _months[lang]!;
    final weekdays = _weekdays[lang]!;
    final canGoBack = DateTime(
      focused.year,
      focused.month,
    ).isAfter(DateTime(first.year, first.month));
    final canGoForward = DateTime(
      focused.year,
      focused.month,
    ).isBefore(DateTime(last.year, last.month));

    return Container(
      key: const Key('slot_calendar'),
      padding: const EdgeInsets.fromLTRB(12, 14, 12, 14),
      decoration: BoxDecoration(
        color: scheme.surfaceContainer,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: scheme.outlineVariant),
      ),
      child: Column(
        children: [
          Row(
            children: [
              IconButton(
                key: const Key('calendar_prev'),
                onPressed: canGoBack
                    ? () => onMonthChanged(
                        DateTime(focused.year, focused.month - 1),
                      )
                    : null,
                color: scheme.primary,
                icon: const Icon(Icons.chevron_left, size: 30),
              ),
              Expanded(
                child: Text(
                  '${months[focused.month - 1]} ${focused.year}',
                  key: const Key('calendar_title'),
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontSize: 18),
                ),
              ),
              IconButton(
                key: const Key('calendar_next'),
                onPressed: canGoForward
                    ? () => onMonthChanged(
                        DateTime(focused.year, focused.month + 1),
                      )
                    : null,
                color: scheme.primary,
                icon: const Icon(Icons.chevron_right, size: 30),
              ),
            ],
          ),
          TableCalendar<void>(
            firstDay: _day(first),
            lastDay: _day(last),
            focusedDay: focused,
            selectedDayPredicate: (d) => isSameDay(d, selected),
            enabledDayPredicate: _bookable,
            startingDayOfWeek: StartingDayOfWeek.monday,
            headerVisible: false,
            availableGestures: AvailableGestures.horizontalSwipe,
            rowHeight: 52,
            onDaySelected: (day, _) => onSelected(_day(day)),
            onPageChanged: onMonthChanged,
            calendarFormat: CalendarFormat.month,
            calendarBuilders: CalendarBuilders(
              dowBuilder: (context, day) => Center(
                child: Text(
                  weekdays[day.weekday - 1],
                  style: TextStyle(color: scheme.onSurfaceVariant),
                ),
              ),
              defaultBuilder: (context, day, _) => _Cell(
                day: day,
                bookable: _bookable(day),
                markUnavailable: _marked(day),
              ),
              todayBuilder: (context, day, _) => _Cell(
                day: day,
                bookable: _bookable(day),
                markUnavailable: _marked(day),
              ),
              disabledBuilder: (context, day, _) => _Cell(
                day: day,
                bookable: false,
                markUnavailable: _marked(day),
              ),
              selectedBuilder: (context, day, _) =>
                  _Cell(day: day, bookable: true, selected: true),
              outsideBuilder: (context, day, _) => const SizedBox.shrink(),
            ),
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              const SizedBox(width: 8),
              Container(
                width: 12,
                height: 12,
                decoration: const BoxDecoration(
                  shape: BoxShape.circle,
                  color: OttomanColors.alert,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  t.unavailableMarkedRed,
                  style: TextStyle(color: scheme.onSurfaceVariant),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  /// A day that is within the booking window but known to be full or closed gets the red dot; past days and days
  /// beyond the window are only dimmed.
  bool _marked(DateTime day) {
    final d = _day(day);
    if (d.isBefore(_day(first)) || d.isAfter(_day(last))) return true;
    return available != null && !available!.contains(d);
  }
}

class _Cell extends StatelessWidget {
  const _Cell({
    required this.day,
    required this.bookable,
    this.markUnavailable = false,
    this.selected = false,
  });

  final DateTime day;
  final bool bookable;
  final bool markUnavailable;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            key: Key(selected ? 'day_selected' : 'day_${day.day}'),
            width: 44,
            height: 44,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: selected ? scheme.primary : null,
            ),
            child: Text(
              '${day.day}',
              style: TextStyle(
                fontSize: 18,
                fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
                color: selected
                    ? scheme.onPrimary
                    : bookable
                    ? scheme.onSurface
                    : scheme.onSurface.withValues(alpha: 0.28),
              ),
            ),
          ),
          if (markUnavailable)
            Container(
              key: Key('dot_${day.day}'),
              margin: const EdgeInsets.only(top: 2),
              width: 5,
              height: 5,
              decoration: const BoxDecoration(
                shape: BoxShape.circle,
                color: OttomanColors.alert,
              ),
            )
          else
            const SizedBox(height: 7),
        ],
      ),
    );
  }
}

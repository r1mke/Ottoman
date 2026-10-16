import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Quick period choices plus a free date range. Reports the chosen range through [onChanged].
class PeriodPicker extends StatefulWidget {
  const PeriodPicker({
    super.key,
    required this.initial,
    required this.onChanged,
    this.now,
  });

  final DateRange initial;
  final ValueChanged<DateRange> onChanged;

  /// Test seam for "today".
  final DateTime? now;

  @override
  State<PeriodPicker> createState() => _PeriodPickerState();
}

enum _Preset { today, week, month30, thisMonth, lastMonth, thisYear, custom }

class _PeriodPickerState extends State<PeriodPicker> {
  _Preset _selected = _Preset.month30;
  late DateRange _range = widget.initial;

  DateTime get _now => widget.now ?? DateTime.now();

  DateRange _rangeFor(_Preset p) => switch (p) {
    _Preset.today => DateRange.today(_now),
    _Preset.week => DateRange.lastDays(_now, 7),
    _Preset.month30 => DateRange.lastDays(_now, 30),
    _Preset.thisMonth => DateRange.thisMonth(_now),
    _Preset.lastMonth => DateRange.lastMonth(_now),
    _Preset.thisYear => DateRange.thisYear(_now),
    _Preset.custom => _range,
  };

  Future<void> _choose(_Preset p) async {
    if (p == _Preset.custom) {
      final picked = await showDateRangePicker(
        context: context,
        firstDate: DateTime(2020),
        lastDate: DateTime(_now.year + 1, 12, 31),
        initialDateRange: DateTimeRange(start: _range.from, end: _range.to),
      );
      if (picked == null) return;
      _range = DateRange(picked.start, picked.end);
    } else {
      _range = _rangeFor(p);
    }
    setState(() => _selected = p);
    widget.onChanged(_range);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final labels = {
      _Preset.today: t.periodToday,
      _Preset.week: t.period7,
      _Preset.month30: t.period30,
      _Preset.thisMonth: t.periodThisMonth,
      _Preset.lastMonth: t.periodLastMonth,
      _Preset.thisYear: t.periodThisYear,
      _Preset.custom: t.periodCustom,
    };
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        for (final p in _Preset.values)
          ChoiceChip(
            key: Key('period_${p.name}'),
            label: Text(labels[p]!),
            selected: _selected == p,
            onSelected: (_) => _choose(p),
          ),
        Text(
          '${_fmt(_range.from)} – ${_fmt(_range.to)}',
          key: const Key('period_text'),
          style: TextStyle(color: Theme.of(context).colorScheme.outline),
        ),
      ],
    );
  }

  static String _fmt(DateTime d) =>
      '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${d.year}.';
}

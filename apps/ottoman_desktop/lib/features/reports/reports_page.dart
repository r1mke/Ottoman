import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'file_saver.dart';
import 'period_picker.dart';

/// A report as a table, with totals, and the three export formats.
class ReportTableView extends StatelessWidget {
  const ReportTableView({super.key, required this.table});

  final ReportTable table;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    if (table.rows.isEmpty) {
      return Center(
        key: const Key('report_empty'),
        child: Text(t.noDataForPeriod),
      );
    }
    return Card(
      clipBehavior: Clip.antiAlias,
      child: SingleChildScrollView(
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: ConstrainedBox(
            constraints: BoxConstraints(
              minWidth: MediaQuery.sizeOf(context).width - 160,
            ),
            child: DataTable(
              key: const Key('report_table'),
              headingRowColor: WidgetStatePropertyAll(
                scheme.surfaceContainerHighest,
              ),
              columnSpacing: 28,
              columns: [
                for (final c in table.columns)
                  DataColumn(label: Text(c.title), numeric: c.isNumeric),
              ],
              rows: [
                for (final row in table.rows)
                  DataRow(
                    cells: [
                      for (var i = 0; i < table.columns.length; i++)
                        DataCell(Text(table.format(row[i], table.columns[i]))),
                    ],
                  ),
                if (table.totals != null)
                  DataRow(
                    key: const ValueKey('report_totals'),
                    color: WidgetStatePropertyAll(scheme.surfaceContainerHigh),
                    cells: [
                      for (var i = 0; i < table.columns.length; i++)
                        DataCell(
                          Text(
                            table.format(table.totals![i], table.columns[i]),
                            style: const TextStyle(fontWeight: FontWeight.w800),
                          ),
                        ),
                    ],
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// "Štampaj" and the "Izvezi" menu: fetch the file from the server, then print it or save it to Downloads.
class ExportMenu extends StatelessWidget {
  const ExportMenu({super.key, required this.fetch, required this.saver});

  /// Downloads the report in the given [ExportKind] format.
  final Future<DownloadedFile> Function(String format) fetch;
  final FileSaver saver;

  Future<void> _run(BuildContext context, String format) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final file = await fetch(format);
      final path = await saver.save(file);
      messenger.showSnackBar(
        SnackBar(
          content: Text(t.savedTo(path)),
          action: SnackBarAction(
            label: t.showInFolder,
            onPressed: () => saver.reveal(path),
          ),
        ),
      );
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  /// Fetches the PDF and hands it to the system print dialog. A printer problem is a message, not a crash.
  Future<void> _print(BuildContext context) async {
    final t = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await saver.printPdf(await fetch(ExportKind.pdf));
    } on AppException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    } on Object {
      messenger.showSnackBar(SnackBar(content: Text(t.printFailed)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        OutlinedButton.icon(
          key: const Key('print_report'),
          onPressed: () => _print(context),
          icon: const Icon(Icons.print_outlined),
          label: Text(t.printReport),
        ),
        const SizedBox(width: 8),
        _exportMenu(context, t),
      ],
    );
  }

  Widget _exportMenu(BuildContext context, AppLocalizations t) {
    return MenuAnchor(
      menuChildren: [
        MenuItemButton(
          key: const Key('export_csv'),
          onPressed: () => _run(context, ExportKind.csv),
          child: Text(t.exportCsv),
        ),
        MenuItemButton(
          key: const Key('export_xlsx'),
          onPressed: () => _run(context, ExportKind.xlsx),
          child: Text(t.exportExcel),
        ),
        MenuItemButton(
          key: const Key('export_pdf'),
          onPressed: () => _run(context, ExportKind.pdf),
          child: Text(t.exportPdf),
        ),
      ],
      builder: (context, controller, _) => OutlinedButton.icon(
        key: const Key('export_menu'),
        onPressed: () =>
            controller.isOpen ? controller.close() : controller.open(),
        icon: const Icon(Icons.download_outlined),
        label: Text(t.export),
      ),
    );
  }
}

/// One of the fixed reports for a chosen period.
class ReportView extends StatefulWidget {
  const ReportView({
    super.key,
    required this.api,
    required this.report,
    required this.saver,
    this.now,
  });

  final ReportsApi api;
  final String report;
  final FileSaver saver;
  final DateTime? now;

  @override
  State<ReportView> createState() => _ReportViewState();
}

class _ReportViewState extends State<ReportView> {
  late DateRange _range = DateRange.lastDays(widget.now ?? DateTime.now(), 30);
  int _version = 0;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: PeriodPicker(
                initial: _range,
                now: widget.now,
                onChanged: (r) => setState(() {
                  _range = r;
                  _version++;
                }),
              ),
            ),
            ExportMenu(
              saver: widget.saver,
              fetch: (format) => widget.api.export(
                widget.report,
                _range.from,
                _range.to,
                format,
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        Expanded(
          child: AsyncView<ReportTable>(
            key: ValueKey('${widget.report}-$_version'),
            load: () => widget.api.table(widget.report, _range.from, _range.to),
            skeleton: const ListSkeleton(),
            builder: (context, table, _) => ReportTableView(table: table),
          ),
        ),
      ],
    );
  }
}

/// "Zarada": the revenue report on its own.
class EarningsPage extends StatelessWidget {
  const EarningsPage({
    super.key,
    required this.api,
    this.saver = const DownloadsFileSaver(),
    this.now,
  });

  final ReportsApi api;
  final FileSaver saver;
  final DateTime? now;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
    child: ReportView(api: api, report: 'revenue', saver: saver, now: now),
  );
}

/// "Izvještaji": ready-made reports plus a builder where fields and filters are picked from a menu.
class ReportsPage extends StatefulWidget {
  const ReportsPage({
    super.key,
    required this.api,
    this.saver = const DownloadsFileSaver(),
    this.now,
  });

  final ReportsApi api;
  final FileSaver saver;
  final DateTime? now;

  @override
  State<ReportsPage> createState() => _ReportsPageState();
}

class _ReportsPageState extends State<ReportsPage> {
  static const _custom = 'custom';
  String _selected = 'employees';

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final labels = {
      'employees': t.reportEmployees,
      'revenue': t.reportRevenue,
      'services': t.reportServices,
      'products': t.reportProducts,
      'reservations': t.reportReservations,
      'orders': t.reportOrders,
      _custom: t.reportCustom,
    };
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final e in labels.entries)
                ChoiceChip(
                  key: Key('report_${e.key}'),
                  label: Text(e.value),
                  selected: _selected == e.key,
                  onSelected: (_) => setState(() => _selected = e.key),
                ),
            ],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: _selected == _custom
                ? CustomReportBuilder(
                    api: widget.api,
                    saver: widget.saver,
                    now: widget.now,
                  )
                : ReportView(
                    key: ValueKey(_selected),
                    api: widget.api,
                    report: _selected,
                    saver: widget.saver,
                    now: widget.now,
                  ),
          ),
        ],
      ),
    );
  }
}

class CustomReportBuilder extends StatefulWidget {
  const CustomReportBuilder({
    super.key,
    required this.api,
    required this.saver,
    this.now,
  });

  final ReportsApi api;
  final FileSaver saver;
  final DateTime? now;

  @override
  State<CustomReportBuilder> createState() => _CustomReportBuilderState();
}

class _CustomReportBuilderState extends State<CustomReportBuilder> {
  late final Future<List<CustomSource>> _sources = widget.api.customSources();
  late DateRange _range = DateRange.lastDays(widget.now ?? DateTime.now(), 30);
  CustomSource? _source;
  final _columns = <String>{};
  String? _status;

  ReportTable? _table;
  String? _error;
  bool _loading = false;

  CustomReportQuery get _query => CustomReportQuery(
    source: _source!.source,
    columns: [
      // keep the order of the menu, not of the clicks
      for (final c in _source!.columns)
        if (_columns.contains(c.key)) c.key,
    ],
    from: _range.from,
    to: _range.to,
    status: _status,
  );

  void _pick(CustomSource s) => setState(() {
    _source = s;
    _columns
      ..clear()
      ..addAll(s.columns.map((c) => c.key));
    _status = null;
    _table = null;
    _error = null;
  });

  Future<void> _run() async {
    if (_source == null || _columns.isEmpty) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final table = await widget.api.custom(_query);
      if (mounted) setState(() => _table = table);
    } on AppException catch (e) {
      // The server explains what it refused (unknown field, bad period, ...).
      if (mounted) {
        setState(() {
          _table = null;
          _error = e.message;
        });
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return FutureBuilder<List<CustomSource>>(
      future: _sources,
      builder: (context, snap) {
        final sources = snap.data;
        if (sources == null) {
          return snap.hasError
              ? ErrorView(
                  message: snap.error is AppException
                      ? (snap.error as AppException).message
                      : AppLocalizations.of(context).genericError,
                  onRetry: () => setState(() {}),
                )
              : const ListSkeleton();
        }
        if (_source == null) {
          // first build: start with every field selected (clearing them later must stick)
          _source = sources.first;
          _columns.addAll(_source!.columns.map((c) => c.key));
        }
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 16,
              runSpacing: 12,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                SizedBox(
                  width: 220,
                  child: DropdownButtonFormField<String>(
                    key: const Key('custom_source'),
                    initialValue: _source!.source,
                    decoration: InputDecoration(
                      labelText: t.chooseSource,
                      isDense: true,
                    ),
                    items: [
                      for (final s in sources)
                        DropdownMenuItem(value: s.source, child: Text(s.title)),
                    ],
                    onChanged: (v) =>
                        _pick(sources.firstWhere((s) => s.source == v)),
                  ),
                ),
                SizedBox(
                  width: 220,
                  child: DropdownButtonFormField<String?>(
                    key: ValueKey('status_${_source!.source}'),
                    initialValue: _status,
                    decoration: InputDecoration(
                      labelText: t.status,
                      isDense: true,
                    ),
                    items: [
                      DropdownMenuItem(
                        value: null,
                        child: Text(t.allStatusesOption),
                      ),
                      for (final s in _source!.statuses)
                        DropdownMenuItem(value: s, child: Text(s)),
                    ],
                    onChanged: (v) => _status = v,
                  ),
                ),
                PeriodPicker(
                  initial: _range,
                  now: widget.now,
                  onChanged: (r) => _range = r,
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text(t.chooseFields),
            Wrap(
              spacing: 8,
              children: [
                for (final c in _source!.columns)
                  FilterChip(
                    key: Key('field_${c.key}'),
                    label: Text(c.title),
                    selected: _columns.contains(c.key),
                    onSelected: (on) => setState(
                      () => on ? _columns.add(c.key) : _columns.remove(c.key),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                FilledButton.icon(
                  key: const Key('run_report'),
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(180, 44),
                  ),
                  onPressed: _columns.isEmpty || _loading ? null : _run,
                  icon: const Icon(Icons.play_arrow),
                  label: Text(t.runReport),
                ),
                const Spacer(),
                if (_table != null)
                  ExportMenu(
                    saver: widget.saver,
                    fetch: (format) => widget.api.exportCustom(_query, format),
                  ),
              ],
            ),
            const SizedBox(height: 12),
            Expanded(
              child: _loading
                  ? const ListSkeleton()
                  : _error != null
                  ? Text(
                      _error!,
                      key: const Key('custom_error'),
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    )
                  : _table == null
                  ? const SizedBox.shrink()
                  : ReportTableView(table: _table!),
            ),
          ],
        );
      },
    );
  }
}

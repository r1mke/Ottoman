import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'period_picker.dart';

/// Admin dashboard ("Pregled"): the numbers that tell how the salon is doing for a chosen period.
class OverviewPage extends StatefulWidget {
  const OverviewPage({super.key, required this.api, this.now});

  final ReportsApi api;
  final DateTime? now;

  @override
  State<OverviewPage> createState() => _OverviewPageState();
}

class _OverviewPageState extends State<OverviewPage> {
  late DateRange _range = DateRange.lastDays(widget.now ?? DateTime.now(), 30);
  int _version = 0;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          PeriodPicker(
            initial: _range,
            now: widget.now,
            onChanged: (r) => setState(() {
              _range = r;
              _version++;
            }),
          ),
          const SizedBox(height: 16),
          Expanded(
            child: AsyncView<Dashboard>(
              key: ValueKey('$_version-${_range.from}-${_range.to}'),
              load: () => widget.api.dashboard(_range.from, _range.to),
              skeleton: const ListSkeleton(),
              builder: (context, d, _) => _Dashboard(data: d, t: t),
            ),
          ),
        ],
      ),
    );
  }
}

class _Dashboard extends StatelessWidget {
  const _Dashboard({required this.data, required this.t});

  final Dashboard data;
  final AppLocalizations t;

  @override
  Widget build(BuildContext context) {
    final d = data;
    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 16,
            runSpacing: 16,
            children: [
              _KpiCard(
                id: 'revenue',
                title: t.kpiRevenue,
                value: money(t, d.revenue.value),
                change: d.revenue.changePercent,
                t: t,
                emphasis: true,
              ),
              _KpiCard(
                id: 'services',
                title: t.kpiServices,
                value: money(t, d.servicesRevenue.value),
                change: d.servicesRevenue.changePercent,
                t: t,
              ),
              _KpiCard(
                id: 'products',
                title: t.kpiProducts,
                value: money(t, d.productsRevenue.value),
                change: d.productsRevenue.changePercent,
                t: t,
              ),
              _KpiCard(
                id: 'appointments',
                title: t.kpiAppointments,
                value: d.appointments.value.toStringAsFixed(0),
                change: d.appointments.changePercent,
                t: t,
              ),
              _KpiCard(
                id: 'orders',
                title: t.kpiOrders,
                value: d.orders.value.toStringAsFixed(0),
                change: d.orders.changePercent,
                t: t,
              ),
              _KpiCard(
                id: 'clients',
                title: t.kpiNewClients,
                value: d.newClients.value.toStringAsFixed(0),
                change: d.newClients.changePercent,
                t: t,
              ),
              _KpiCard(
                id: 'ticket',
                title: t.kpiAverageTicket,
                value: money(t, d.averageTicket),
                t: t,
              ),
              _KpiCard(
                id: 'noshow',
                title: t.kpiNoShow,
                value: '${d.noShowRate}%',
                t: t,
              ),
              _KpiCard(
                id: 'cancelled',
                title: t.kpiCancelled,
                value: '${d.cancellationRate}%',
                t: t,
              ),
            ],
          ),
          const SizedBox(height: 24),
          Text(t.revenueOverTime, style: _heading(context)),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 24, 16, 12),
              child: SizedBox(
                height: 280,
                child: d.series.every((s) => s.total == 0)
                    ? Center(
                        key: const Key('chart_empty'),
                        child: Text(t.noDataForPeriod),
                      )
                    : RevenueChart(series: d.series, t: t),
              ),
            ),
          ),
          const SizedBox(height: 24),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _RankedList(
                  id: 'top_services',
                  title: t.topServices,
                  items: d.topServices,
                  t: t,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: _RankedList(
                  id: 'top_products',
                  title: t.topProducts,
                  items: d.topProducts,
                  t: t,
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),
          Text(t.employeePerformance, style: _heading(context)),
          const SizedBox(height: 12),
          Card(
            clipBehavior: Clip.antiAlias,
            child: SizedBox(
              width: double.infinity,
              child: DataTable(
                key: const Key('employee_table'),
                columns: [
                  DataColumn(label: Text(t.employee)),
                  DataColumn(label: Text(t.completedShort), numeric: true),
                  DataColumn(label: Text(t.noShowShort), numeric: true),
                  DataColumn(label: Text(t.cancelledShort), numeric: true),
                  DataColumn(label: Text(t.revenueShort), numeric: true),
                  DataColumn(label: Text(t.ratingShort), numeric: true),
                ],
                rows: [
                  for (final e in d.employees)
                    DataRow(
                      cells: [
                        DataCell(Text(e.name)),
                        DataCell(Text('${e.completed}')),
                        DataCell(Text('${e.noShow}')),
                        DataCell(Text('${e.cancelled}')),
                        DataCell(Text(money(t, e.revenue))),
                        DataCell(
                          Text(
                            e.averageRating == null
                                ? '-'
                                : '${e.averageRating!.toStringAsFixed(1)} (${e.reviewCount})',
                          ),
                        ),
                      ],
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  TextStyle? _heading(BuildContext context) =>
      Theme.of(context).textTheme.titleMedium
          ?.copyWith(fontWeight: FontWeight.w700);
}

class _KpiCard extends StatelessWidget {
  const _KpiCard({
    required this.id,
    required this.title,
    required this.value,
    required this.t,
    this.change,
    this.emphasis = false,
  });

  final String id;
  final String title;
  final String value;
  final double? change;
  final AppLocalizations t;
  final bool emphasis;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return SizedBox(
      width: 230,
      child: Card(
        key: Key('kpi_$id'),
        color: emphasis ? scheme.primary : null,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: TextStyle(
                  color: emphasis ? scheme.onPrimary : scheme.outline,
                  fontSize: 12,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                value,
                key: Key('kpi_value_$id'),
                style: TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.w800,
                  color: emphasis ? scheme.onPrimary : null,
                ),
              ),
              const SizedBox(height: 4),
              if (change != null)
                Row(
                  children: [
                    Icon(
                      change! >= 0 ? Icons.arrow_upward : Icons.arrow_downward,
                      size: 14,
                      color: _color(change!, emphasis, scheme),
                    ),
                    const SizedBox(width: 2),
                    Text(
                      '${change!.abs().toStringAsFixed(1)}%',
                      key: Key('kpi_change_$id'),
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                        color: _color(change!, emphasis, scheme),
                      ),
                    ),
                  ],
                )
              else
                const SizedBox(height: 16),
            ],
          ),
        ),
      ),
    );
  }

  static Color _color(double change, bool emphasis, ColorScheme scheme) {
    if (emphasis) return scheme.onPrimary;
    return change >= 0 ? Colors.green.shade700 : Colors.red.shade700;
  }
}

class _RankedList extends StatelessWidget {
  const _RankedList({
    required this.id,
    required this.title,
    required this.items,
    required this.t,
  });

  final String id;
  final String title;
  final List<RankedItem> items;
  final AppLocalizations t;

  @override
  Widget build(BuildContext context) {
    final top = items.isEmpty
        ? 1.0
        : items.map((i) => i.revenue).reduce((a, b) => a > b ? a : b);
    return Card(
      key: Key(id),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: Theme.of(context).textTheme.titleMedium
                  ?.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 12),
            if (items.isEmpty)
              Text(t.noDataForPeriod)
            else
              for (final i in items)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              '${i.name} · ${i.quantity}×',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                          Text(
                            money(t, i.revenue),
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      LinearProgressIndicator(
                        value: top == 0 ? 0 : i.revenue / top,
                        minHeight: 6,
                        borderRadius: BorderRadius.circular(3),
                      ),
                    ],
                  ),
                ),
          ],
        ),
      ),
    );
  }
}

/// Stacked bars: services at the bottom, products on top. Hover shows the exact amounts.
class RevenueChart extends StatelessWidget {
  const RevenueChart({super.key, required this.series, required this.t});

  final List<SeriesPoint> series;
  final AppLocalizations t;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final servicesColor = scheme.primary;
    final productsColor = Colors.amber.shade700;
    final every = (series.length / 10).ceil().clamp(1, 1000);
    final maxY = series.map((s) => s.total).reduce((a, b) => a > b ? a : b);

    return BarChart(
      key: const Key('revenue_chart'),
      BarChartData(
        maxY: maxY * 1.15,
        alignment: BarChartAlignment.spaceAround,
        gridData: FlGridData(
          drawVerticalLine: false,
          getDrawingHorizontalLine: (_) =>
              FlLine(color: scheme.outlineVariant, strokeWidth: 0.5),
        ),
        borderData: FlBorderData(show: false),
        titlesData: FlTitlesData(
          topTitles: const AxisTitles(),
          rightTitles: const AxisTitles(),
          leftTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 48,
              getTitlesWidget: (v, meta) => Text(
                v == meta.max ? '' : v.toStringAsFixed(0),
                style: TextStyle(fontSize: 10, color: scheme.outline),
              ),
            ),
          ),
          bottomTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 28,
              getTitlesWidget: (v, meta) {
                final i = v.toInt();
                if (i < 0 || i >= series.length || i % every != 0) {
                  return const SizedBox.shrink();
                }
                return Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Text(
                    series[i].label,
                    style: TextStyle(fontSize: 10, color: scheme.outline),
                  ),
                );
              },
            ),
          ),
        ),
        barTouchData: BarTouchData(
          touchTooltipData: BarTouchTooltipData(
            getTooltipItem: (group, _, rod, _) {
              final s = series[group.x];
              return BarTooltipItem(
                '${s.label}\n${t.kpiServices}: ${money(t, s.services)}\n${t.kpiProducts}: ${money(t, s.products)}',
                const TextStyle(color: Colors.white, fontSize: 12),
              );
            },
          ),
        ),
        barGroups: [
          for (var i = 0; i < series.length; i++)
            BarChartGroupData(
              x: i,
              barRods: [
                BarChartRodData(
                  toY: series[i].total,
                  width: series.length > 40 ? 6 : 14,
                  borderRadius: const BorderRadius.vertical(
                    top: Radius.circular(3),
                  ),
                  rodStackItems: [
                    BarChartRodStackItem(0, series[i].services, servicesColor),
                    BarChartRodStackItem(
                      series[i].services,
                      series[i].total,
                      productsColor,
                    ),
                  ],
                ),
              ],
            ),
        ],
      ),
    );
  }
}

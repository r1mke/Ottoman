import '../network/api_client.dart';

String _date(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

double? _optDouble(Object? v) => v == null ? null : (v as num).toDouble();

/// A value with its change against the period before (null when there was nothing to compare with).
class Kpi {
  const Kpi(this.value, this.changePercent);
  final double value;
  final double? changePercent;

  factory Kpi.fromJson(Map<String, dynamic> j) =>
      Kpi((j['value'] as num).toDouble(), _optDouble(j['changePercent']));
}

class SeriesPoint {
  const SeriesPoint({
    required this.start,
    required this.label,
    required this.services,
    required this.products,
    required this.total,
  });

  final DateTime start;
  final String label;
  final double services;
  final double products;
  final double total;

  factory SeriesPoint.fromJson(Map<String, dynamic> j) => SeriesPoint(
    start: DateTime.parse(j['start'] as String),
    label: j['label'] as String,
    services: (j['services'] as num).toDouble(),
    products: (j['products'] as num).toDouble(),
    total: (j['total'] as num).toDouble(),
  );
}

class RankedItem {
  const RankedItem(this.name, this.quantity, this.revenue);
  final String name;
  final int quantity;
  final double revenue;

  factory RankedItem.fromJson(Map<String, dynamic> j) => RankedItem(
    j['name'] as String,
    j['quantity'] as int,
    (j['revenue'] as num).toDouble(),
  );
}

class EmployeePerformance {
  const EmployeePerformance({
    required this.name,
    required this.completed,
    required this.cancelled,
    required this.noShow,
    required this.revenue,
    required this.reviewCount,
    this.averageRating,
  });

  final String name;
  final int completed;
  final int cancelled;
  final int noShow;
  final double revenue;
  final double? averageRating;
  final int reviewCount;

  factory EmployeePerformance.fromJson(Map<String, dynamic> j) =>
      EmployeePerformance(
        name: j['name'] as String,
        completed: j['completed'] as int,
        cancelled: j['cancelled'] as int,
        noShow: j['noShow'] as int,
        revenue: (j['revenue'] as num).toDouble(),
        averageRating: _optDouble(j['averageRating']),
        reviewCount: j['reviewCount'] as int,
      );
}

class Dashboard {
  const Dashboard({
    required this.from,
    required this.to,
    required this.granularity,
    required this.revenue,
    required this.servicesRevenue,
    required this.productsRevenue,
    required this.appointments,
    required this.orders,
    required this.newClients,
    required this.averageTicket,
    required this.noShowRate,
    required this.cancellationRate,
    required this.series,
    required this.topServices,
    required this.topProducts,
    required this.employees,
  });

  final DateTime from;
  final DateTime to;
  final String granularity;
  final Kpi revenue;
  final Kpi servicesRevenue;
  final Kpi productsRevenue;
  final Kpi appointments;
  final Kpi orders;
  final Kpi newClients;
  final double averageTicket;
  final double noShowRate;
  final double cancellationRate;
  final List<SeriesPoint> series;
  final List<RankedItem> topServices;
  final List<RankedItem> topProducts;
  final List<EmployeePerformance> employees;

  factory Dashboard.fromJson(Map<String, dynamic> j) {
    List<T> list<T>(String key, T Function(Map<String, dynamic>) f) =>
        (j[key] as List).map((e) => f(e as Map<String, dynamic>)).toList();
    return Dashboard(
      from: DateTime.parse(j['from'] as String),
      to: DateTime.parse(j['to'] as String),
      granularity: j['granularity'] as String,
      revenue: Kpi.fromJson(j['revenue'] as Map<String, dynamic>),
      servicesRevenue: Kpi.fromJson(
        j['servicesRevenue'] as Map<String, dynamic>,
      ),
      productsRevenue: Kpi.fromJson(
        j['productsRevenue'] as Map<String, dynamic>,
      ),
      appointments: Kpi.fromJson(j['appointments'] as Map<String, dynamic>),
      orders: Kpi.fromJson(j['orders'] as Map<String, dynamic>),
      newClients: Kpi.fromJson(j['newClients'] as Map<String, dynamic>),
      averageTicket: (j['averageTicket'] as num).toDouble(),
      noShowRate: (j['noShowRate'] as num).toDouble(),
      cancellationRate: (j['cancellationRate'] as num).toDouble(),
      series: list('series', SeriesPoint.fromJson),
      topServices: list('topServices', RankedItem.fromJson),
      topProducts: list('topProducts', RankedItem.fromJson),
      employees: list('employees', EmployeePerformance.fromJson),
    );
  }
}

/// Column kinds as sent by the API.
abstract final class ColumnKind {
  static const text = 'Text';
  static const number = 'Number';
  static const money = 'Money';
  static const date = 'Date';
  static const percent = 'Percent';
}

class ReportColumn {
  const ReportColumn(this.key, this.title, this.type);
  final String key;
  final String title;
  final String type;

  bool get isNumeric =>
      type == ColumnKind.number ||
      type == ColumnKind.money ||
      type == ColumnKind.percent;

  factory ReportColumn.fromJson(Map<String, dynamic> j) => ReportColumn(
    j['key'] as String,
    j['title'] as String,
    j['type'] as String,
  );
}

/// A report as rows and columns - what the screens show and what the exports are made of.
class ReportTable {
  const ReportTable(this.title, this.columns, this.rows, this.totals);
  final String title;
  final List<ReportColumn> columns;
  final List<List<Object?>> rows;
  final List<Object?>? totals;

  /// "12.50" for money, whole numbers as is, text unchanged.
  String format(Object? value, ReportColumn column) {
    if (value == null) return '';
    if (value is num) {
      return column.type == ColumnKind.money || value is double
          ? value.toStringAsFixed(2)
          : value.toString();
    }
    return value.toString();
  }

  factory ReportTable.fromJson(Map<String, dynamic> j) => ReportTable(
    j['title'] as String,
    (j['columns'] as List)
        .map((e) => ReportColumn.fromJson(e as Map<String, dynamic>))
        .toList(),
    (j['rows'] as List).map((r) => List<Object?>.from(r as List)).toList(),
    j['totals'] == null ? null : List<Object?>.from(j['totals'] as List),
  );
}

class CustomSource {
  const CustomSource(this.source, this.title, this.columns, this.statuses);
  final String source;
  final String title;
  final List<ReportColumn> columns;
  final List<String> statuses;

  factory CustomSource.fromJson(Map<String, dynamic> j) => CustomSource(
    j['source'] as String,
    j['title'] as String,
    (j['columns'] as List)
        .map((e) => ReportColumn.fromJson(e as Map<String, dynamic>))
        .toList(),
    (j['statuses'] as List).cast<String>(),
  );
}

/// A request for the report builder. Only choices from the server's menu are accepted.
class CustomReportQuery {
  const CustomReportQuery({
    required this.source,
    required this.columns,
    required this.from,
    required this.to,
    this.status,
    this.employeeId,
  });

  final String source;
  final List<String> columns;
  final DateTime from;
  final DateTime to;
  final String? status;
  final String? employeeId;

  Map<String, dynamic> toJson() => {
    'source': source,
    'columns': columns,
    'from': _date(from),
    'to': _date(to),
    'status': status,
    'employeeId': employeeId,
  };
}

abstract final class ExportKind {
  static const csv = 'Csv';
  static const xlsx = 'Xlsx';
  static const pdf = 'Pdf';
}

/// Admin-only reports. [from] and [to] are salon-local calendar days (both inclusive).
class ReportsApi {
  ReportsApi(this._api);

  final ApiClient _api;

  Future<Dashboard> dashboard(DateTime from, DateTime to) async =>
      Dashboard.fromJson(
        await _api.get<Map<String, dynamic>>(
          '/api/v1/reports/dashboard',
          query: {'from': _date(from), 'to': _date(to)},
        ),
      );

  /// [report]: revenue, employees, services, products, reservations, orders.
  Future<ReportTable> table(String report, DateTime from, DateTime to) async =>
      ReportTable.fromJson(
        await _api.get<Map<String, dynamic>>(
          '/api/v1/reports/$report',
          query: {'from': _date(from), 'to': _date(to)},
        ),
      );

  Future<DownloadedFile> export(
    String report,
    DateTime from,
    DateTime to,
    String format,
  ) => _api.download(
    '/api/v1/reports/$report/export',
    query: {'from': _date(from), 'to': _date(to), 'format': format},
  );

  Future<List<CustomSource>> customSources() async =>
      (await _api.get<List<dynamic>>('/api/v1/reports/custom/fields'))
          .map((e) => CustomSource.fromJson(e as Map<String, dynamic>))
          .toList();

  Future<ReportTable> custom(CustomReportQuery q) async => ReportTable.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/reports/custom',
      body: q.toJson(),
    ),
  );

  Future<DownloadedFile> exportCustom(CustomReportQuery q, String format) =>
      _api.download(
        '/api/v1/reports/custom/export',
        method: 'POST',
        query: {'format': format},
        body: q.toJson(),
      );
}

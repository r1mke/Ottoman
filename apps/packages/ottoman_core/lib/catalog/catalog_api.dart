import '../network/api_client.dart';
import 'catalog_models.dart';

/// Services, employees and availability endpoints.
class CatalogApi {
  CatalogApi(this._api);

  final ApiClient _api;

  // ---- services

  Future<List<Service>> services({
    String? employeeId,
    bool includeInactive = false,
  }) async {
    final data = await _api.get<List<dynamic>>(
      '/api/v1/services',
      query: {
        'employeeId': ?employeeId,
        if (includeInactive) 'includeInactive': true,
      },
    );
    return data
        .map((e) => Service.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<Service> saveService({
    int? id,
    required String name,
    String? description,
    required double price,
    required int durationMinutes,
    bool isActive = true,
  }) async {
    final body = {
      'name': name,
      'description': description,
      'price': price,
      'durationMinutes': durationMinutes,
      'isActive': isActive,
    };
    final data = id == null
        ? await _api.post<Map<String, dynamic>>('/api/v1/services', body: body)
        : await _api.put<Map<String, dynamic>>(
            '/api/v1/services/$id',
            body: body,
          );
    return Service.fromJson(data);
  }

  Future<void> deactivateService(int id) =>
      _api.delete<dynamic>('/api/v1/services/$id');

  // ---- employees

  Future<List<Employee>> employees({bool includeInactive = false}) async {
    final data = await _api.get<List<dynamic>>(
      '/api/v1/employees',
      query: {if (includeInactive) 'includeInactive': true},
    );
    return data
        .map((e) => Employee.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<Employee> createEmployee({
    required String firstName,
    required String lastName,
    required String email,
    String? phone,
    required String password,
    required String title,
    String? bio,
    List<int> serviceIds = const [],
  }) async => Employee.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/employees',
      body: {
        'firstName': firstName,
        'lastName': lastName,
        'email': email,
        'phone': phone,
        'password': password,
        'title': title,
        'bio': bio,
        'serviceIds': serviceIds,
      },
    ),
  );

  Future<Employee> updateEmployee(
    String id, {
    required String firstName,
    required String lastName,
    String? phone,
    required String title,
    String? bio,
    required bool isActive,
  }) async => Employee.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/employees/$id',
      body: {
        'firstName': firstName,
        'lastName': lastName,
        'phone': phone,
        'title': title,
        'bio': bio,
        'isActive': isActive,
      },
    ),
  );

  Future<Employee> setEmployeeServices(String id, List<int> serviceIds) async =>
      Employee.fromJson(
        await _api.put<Map<String, dynamic>>(
          '/api/v1/employees/$id/services',
          body: {'serviceIds': serviceIds},
        ),
      );

  Future<List<WorkingHours>> workingHours(String id) async {
    final data = await _api.get<List<dynamic>>(
      '/api/v1/employees/$id/working-hours',
    );
    return data
        .map((e) => WorkingHours.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<WorkingHours>> setWorkingHours(
    String id,
    List<WorkingHours> days,
  ) async {
    final data = await _api.put<List<dynamic>>(
      '/api/v1/employees/$id/working-hours',
      body: {'days': days.map((d) => d.toJson()).toList()},
    );
    return data
        .map((e) => WorkingHours.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  // ---- availability

  /// [date] is the salon-local calendar date. [allTimes] lists every 5-minute start.
  Future<Availability> availability({
    required String employeeId,
    required DateTime date,
    required List<int> serviceIds,
    bool allTimes = false,
  }) async {
    final d =
        '${date.year.toString().padLeft(4, '0')}-'
        '${date.month.toString().padLeft(2, '0')}-'
        '${date.day.toString().padLeft(2, '0')}';
    return Availability.fromJson(
      await _api.get<Map<String, dynamic>>(
        '/api/v1/employees/$employeeId/availability',
        query: {'date': d, 'serviceIds': serviceIds, 'allTimes': allTimes},
      ),
    );
  }
}

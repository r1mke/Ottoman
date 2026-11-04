import '../network/api_client.dart';

/// Opening time of one weekday; [day] is 0 = Sunday ... 6 = Saturday, times are minutes after midnight.
/// Both null means closed.
class OpeningHours {
  const OpeningHours({required this.day, this.openMinutes, this.closeMinutes});

  final int day;
  final int? openMinutes;
  final int? closeMinutes;

  bool get isClosed => openMinutes == null || closeMinutes == null;

  OpeningHours copyWith({
    int? openMinutes,
    int? closeMinutes,
    bool closed = false,
  }) => OpeningHours(
    day: day,
    openMinutes: closed ? null : (openMinutes ?? this.openMinutes),
    closeMinutes: closed ? null : (closeMinutes ?? this.closeMinutes),
  );

  factory OpeningHours.fromJson(Map<String, dynamic> j) => OpeningHours(
    day: j['day'] as int,
    openMinutes: j['openMinutes'] as int?,
    closeMinutes: j['closeMinutes'] as int?,
  );

  Map<String, dynamic> toJson() => {
    'day': day,
    'openMinutes': openMinutes,
    'closeMinutes': closeMinutes,
  };
}

/// What the home screen shows about the salon.
class SalonProfile {
  const SalonProfile({
    required this.name,
    this.description,
    this.address,
    this.phone,
    this.mapsUrl,
    this.logoUrl,
    this.latitude,
    this.longitude,
    required this.hours,
    this.instagramUrl,
    this.facebookUrl,
    this.autoConfirmReservations = true,
  });

  final String name;
  final String? description;
  final String? address;
  final String? phone;
  final String? mapsUrl;
  final String? logoUrl;
  final double? latitude;
  final double? longitude;
  final String? instagramUrl;
  final String? facebookUrl;

  /// Off: a client's booking waits for a barber or the admin to confirm or decline it.
  final bool autoConfirmReservations;

  bool get hasLocation => latitude != null && longitude != null;

  /// Monday first, Sunday last.
  final List<OpeningHours> hours;

  factory SalonProfile.fromJson(Map<String, dynamic> j) => SalonProfile(
    name: j['name'] as String,
    description: j['description'] as String?,
    address: j['address'] as String?,
    phone: j['phone'] as String?,
    mapsUrl: j['mapsUrl'] as String?,
    logoUrl: j['logoUrl'] as String?,
    latitude: (j['latitude'] as num?)?.toDouble(),
    longitude: (j['longitude'] as num?)?.toDouble(),
    hours: (j['hours'] as List)
        .map((e) => OpeningHours.fromJson(e as Map<String, dynamic>))
        .toList(),
    instagramUrl: j['instagramUrl'] as String?,
    facebookUrl: j['facebookUrl'] as String?,
    autoConfirmReservations: (j['autoConfirmReservations'] as bool?) ?? true,
  );
}

class SalonApi {
  SalonApi(this._api);

  final ApiClient _api;

  Future<SalonProfile> profile() async => SalonProfile.fromJson(
    await _api.get<Map<String, dynamic>>('/api/v1/salon'),
  );

  Future<SalonProfile> save({
    required String name,
    String? description,
    String? address,
    String? phone,
    String? mapsUrl,
    double? latitude,
    double? longitude,
    required List<OpeningHours> hours,
    String? instagramUrl,
    String? facebookUrl,
    bool? autoConfirmReservations,
  }) async => SalonProfile.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/salon',
      body: {
        'name': name,
        'description': description,
        'address': address,
        'phone': phone,
        'mapsUrl': mapsUrl,
        'latitude': latitude,
        'longitude': longitude,
        'instagramUrl': instagramUrl,
        'facebookUrl': facebookUrl,
        'autoConfirmReservations': ?autoConfirmReservations,
        'hours': hours.map((h) => h.toJson()).toList(),
      },
    ),
  );

  Future<SalonProfile> setLogo(List<int> bytes, String filename) async =>
      SalonProfile.fromJson(
        await _api.put<Map<String, dynamic>>(
          '/api/v1/salon/logo',
          body: imageForm(bytes, filename),
        ),
      );

  Future<SalonProfile> clearLogo() async => SalonProfile.fromJson(
    await _api.delete<Map<String, dynamic>>('/api/v1/salon/logo'),
  );
}

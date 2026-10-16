class Service {
  const Service({
    required this.id,
    required this.name,
    required this.price,
    required this.durationMinutes,
    required this.isActive,
    this.description,
    this.imageUrl,
  });

  final int id;
  final String name;
  final String? description;
  final double price;
  final int durationMinutes;
  final String? imageUrl;
  final bool isActive;

  factory Service.fromJson(Map<String, dynamic> j) => Service(
    id: j['id'] as int,
    name: j['name'] as String,
    description: j['description'] as String?,
    price: (j['price'] as num).toDouble(),
    durationMinutes: j['durationMinutes'] as int,
    imageUrl: j['imageUrl'] as String?,
    isActive: j['isActive'] as bool,
  );
}

class Employee {
  const Employee({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.title,
    required this.isActive,
    required this.serviceIds,
    this.bio,
    this.photoUrl,
    this.email,
    this.phone,
  });

  final String id;
  final String firstName;
  final String lastName;
  final String title;
  final String? bio;
  final String? photoUrl;
  final bool isActive;
  final List<int> serviceIds;
  final String? email;
  final String? phone;

  String get fullName => '$firstName $lastName'.trim();

  String get initials =>
      '${firstName.isEmpty ? '' : firstName[0]}${lastName.isEmpty ? '' : lastName[0]}'
          .toUpperCase();

  factory Employee.fromJson(Map<String, dynamic> j) => Employee(
    id: j['id'] as String,
    firstName: j['firstName'] as String,
    lastName: j['lastName'] as String,
    title: j['title'] as String,
    bio: j['bio'] as String?,
    photoUrl: j['photoUrl'] as String?,
    isActive: j['isActive'] as bool,
    serviceIds: (j['serviceIds'] as List).cast<int>(),
    email: j['email'] as String?,
    phone: j['phone'] as String?,
  );
}

/// Local salon wall-clock times as "HH:mm".
class WorkingHours {
  const WorkingHours({
    required this.dayOfWeek,
    required this.start,
    required this.end,
    this.breakStart,
    this.breakEnd,
  });

  /// 0 = Sunday ... 6 = Saturday (matches .NET `DayOfWeek`).
  final int dayOfWeek;
  final String start;
  final String end;
  final String? breakStart;
  final String? breakEnd;

  factory WorkingHours.fromJson(Map<String, dynamic> j) => WorkingHours(
    dayOfWeek: j['dayOfWeek'] as int,
    start: j['start'] as String,
    end: j['end'] as String,
    breakStart: j['breakStart'] as String?,
    breakEnd: j['breakEnd'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'dayOfWeek': dayOfWeek,
    'start': start,
    'end': end,
    'breakStart': breakStart,
    'breakEnd': breakEnd,
  };
}

class Availability {
  const Availability({
    required this.date,
    required this.durationMinutes,
    required this.totalPrice,
    required this.allTimes,
    required this.starts,
    required this.localTimes,
  });

  final String date;
  final int durationMinutes;
  final double totalPrice;
  final bool allTimes;

  /// UTC instants.
  final List<DateTime> starts;

  /// Salon-local "HH:mm" label for each entry of [starts] (same order).
  final List<String> localTimes;

  factory Availability.fromJson(Map<String, dynamic> j) => Availability(
    date: j['date'] as String,
    durationMinutes: j['durationMinutes'] as int,
    totalPrice: (j['totalPrice'] as num).toDouble(),
    allTimes: j['allTimes'] as bool,
    starts: (j['starts'] as List)
        .map((e) => DateTime.parse(e as String).toUtc())
        .toList(),
    localTimes: (j['localTimes'] as List).cast<String>(),
  );
}

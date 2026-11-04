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

/// One entry of the pick list of barber titles. [inUse] is how many barbers have it.
class EmployeeTitle {
  const EmployeeTitle({
    required this.id,
    required this.name,
    required this.inUse,
  });

  final int id;
  final String name;
  final int inUse;

  factory EmployeeTitle.fromJson(Map<String, dynamic> j) => EmployeeTitle(
    id: j['id'] as int,
    name: j['name'] as String,
    inUse: j['inUse'] as int,
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
    this.gender,
    this.photoZoom = 1.0,
    this.titleId,
  });

  final String id;
  final String firstName;
  final String lastName;

  /// The title's name, for showing; [titleId] is the entry of the shared title list it points at.
  final String title;
  final int? titleId;

  /// "Male" or "Female"; picks the default avatar while there is no photo.
  final String? gender;

  /// How close the photo crops around the face, 1 (default) to 2.
  final double photoZoom;
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
    gender: j['gender'] as String?,
    photoZoom: (j['photoZoom'] as num?)?.toDouble() ?? 1.0,
    titleId: j['titleId'] as int?,
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

/// The days of one month that can still be booked for a barber and a set of services (the calendar dots).
class MonthAvailability {
  const MonthAvailability({required this.month, required this.availableDays});

  /// First day of the month.
  final DateTime month;
  final Set<DateTime> availableDays;

  bool isAvailable(DateTime day) =>
      availableDays.contains(DateTime(day.year, day.month, day.day));

  factory MonthAvailability.fromJson(Map<String, dynamic> j) {
    DateTime day(String s) {
      final p = DateTime.parse(s);
      return DateTime(p.year, p.month, p.day);
    }

    return MonthAvailability(
      month: day(j['month'] as String),
      availableDays: {
        for (final d in (j['availableDays'] as List)) day(d as String),
      },
    );
  }
}

class AppUser {
  const AppUser({
    required this.id,
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.roles,
    this.phone,
    this.photoUrl,
    this.gender,
    this.bio,
    this.photoZoom,
  });

  final String id;
  final String email;
  final String firstName;
  final String lastName;
  final String? phone;
  final List<String> roles;

  /// Only barbers have these: their photo, and "Male" / "Female" for the default avatar.
  final String? photoUrl;
  final String? gender;

  /// A barber's own text about themselves and how close their avatar crops around the face (1 to 2).
  final String? bio;
  final double? photoZoom;

  String get fullName => '$firstName $lastName'.trim();
  bool hasRole(String role) => roles.contains(role);
  bool get isAdmin => hasRole(Roles.admin);
  bool get isEmployee => hasRole(Roles.employee);
  bool get isStaff => isAdmin || isEmployee;
  bool get isClient => hasRole(Roles.client);

  factory AppUser.fromJson(Map<String, dynamic> j) => AppUser(
    id: j['id'] as String,
    email: j['email'] as String,
    firstName: j['firstName'] as String,
    lastName: j['lastName'] as String,
    phone: j['phone'] as String?,
    roles: (j['roles'] as List).cast<String>(),
    photoUrl: j['photoUrl'] as String?,
    gender: j['gender'] as String?,
    bio: j['bio'] as String?,
    photoZoom: (j['photoZoom'] as num?)?.toDouble(),
  );
}

abstract final class Roles {
  static const admin = 'Admin';
  static const employee = 'Employee';
  static const client = 'Client';
}

class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.refreshToken,
    required this.accessTokenExpiresUtc,
    required this.user,
  });

  final String accessToken;
  final String refreshToken;
  final DateTime accessTokenExpiresUtc;
  final AppUser user;

  factory AuthSession.fromJson(Map<String, dynamic> j) => AuthSession(
    accessToken: j['accessToken'] as String,
    refreshToken: j['refreshToken'] as String,
    accessTokenExpiresUtc: DateTime.parse(j['accessTokenExpiresUtc'] as String),
    user: AppUser.fromJson(j['user'] as Map<String, dynamic>),
  );
}

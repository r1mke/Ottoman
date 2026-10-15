class AppUser {
  const AppUser({
    required this.id,
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.roles,
    this.phone,
  });

  final String id;
  final String email;
  final String firstName;
  final String lastName;
  final String? phone;
  final List<String> roles;

  String get fullName => '$firstName $lastName'.trim();
  bool hasRole(String role) => roles.contains(role);
  bool get isAdmin => hasRole(Roles.admin);
  bool get isEmployee => hasRole(Roles.employee);
  bool get isClient => hasRole(Roles.client);

  factory AppUser.fromJson(Map<String, dynamic> j) => AppUser(
    id: j['id'] as String,
    email: j['email'] as String,
    firstName: j['firstName'] as String,
    lastName: j['lastName'] as String,
    phone: j['phone'] as String?,
    roles: (j['roles'] as List).cast<String>(),
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

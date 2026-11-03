import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'auth_models.dart';

/// An account the sign-in screen remembers on this computer, shown as a card. It never holds a password or a token.
/// The picture is the barber's own photo (or, without one, a default by gender); nobody picks an avatar.
@immutable
class SavedProfile {
  const SavedProfile({
    required this.email,
    required this.name,
    this.id,
    this.photoUrl,
    this.gender,
  });

  final String email;
  final String name;

  /// The account id; the same seed the barber lists use, so the card shows the same default avatar.
  final String? id;
  final String? photoUrl;
  final String? gender;

  String get avatarSeed => id ?? email;

  Map<String, dynamic> toJson() => {
    'email': email,
    'name': name,
    'id': id,
    'photoUrl': photoUrl,
    'gender': gender,
  };

  static SavedProfile? tryParse(Object? j) {
    if (j is! Map) return null;
    final email = j['email'], name = j['name'];
    if (email is! String || email.isEmpty) return null;
    return SavedProfile(
      email: email,
      name: name is String && name.trim().isNotEmpty ? name : email,
      id: j['id'] is String ? j['id'] as String : null,
      photoUrl: j['photoUrl'] is String ? j['photoUrl'] as String : null,
      gender: j['gender'] is String ? j['gender'] as String : null,
    );
  }
}

abstract class SavedProfilesStore {
  Future<List<SavedProfile>> load();
  Future<void> save(List<SavedProfile> profiles);
}

class SecureSavedProfilesStore implements SavedProfilesStore {
  SecureSavedProfilesStore({
    FlutterSecureStorage? storage,
    String keyPrefix = 'ottoman',
  }) : _storage = storage ?? const FlutterSecureStorage(),
       _key = '$keyPrefix.saved_profiles';

  final FlutterSecureStorage _storage;
  final String _key;

  @override
  Future<List<SavedProfile>> load() async {
    try {
      final raw = await _storage.read(key: _key);
      if (raw == null) return const [];
      final decoded = jsonDecode(raw);
      if (decoded is! List) return const [];
      return decoded.map(SavedProfile.tryParse).nonNulls.toList();
    } on Object {
      // Unreadable data only costs the convenience of the cards; the user can sign in again.
      return const [];
    }
  }

  @override
  Future<void> save(List<SavedProfile> profiles) => _storage.write(
    key: _key,
    value: jsonEncode(profiles.map((p) => p.toJson()).toList()),
  );
}

class MemorySavedProfilesStore implements SavedProfilesStore {
  MemorySavedProfilesStore([List<SavedProfile> initial = const []])
    : _items = [...initial];

  List<SavedProfile> _items;

  @override
  Future<List<SavedProfile>> load() async => [..._items];

  @override
  Future<void> save(List<SavedProfile> profiles) async =>
      _items = [...profiles];
}

/// The cards on the sign-in screen: whoever signed in on this computer before. Matched by e-mail, ignoring case.
class SavedProfiles extends ChangeNotifier {
  SavedProfiles(this._store);

  final SavedProfilesStore _store;
  List<SavedProfile> _items = const [];

  List<SavedProfile> get items => _items;

  Future<void> load() async {
    _items = await _store.load();
    notifyListeners();
  }

  /// Adds the account, or refreshes its name and picture (they may have changed since the last sign-in).
  Future<void> remember(AppUser user) async {
    final profile = SavedProfile(
      email: user.email,
      name: user.fullName.isEmpty ? user.email : user.fullName,
      id: user.id,
      photoUrl: user.photoUrl,
      gender: user.gender,
    );
    final i = _indexOf(user.email);
    final next = [..._items];
    if (i < 0) {
      next.add(profile);
    } else {
      next[i] = profile;
    }
    await _commit(next);
  }

  Future<void> remove(String email) async {
    final i = _indexOf(email);
    if (i < 0) return;
    await _commit([..._items]..removeAt(i));
  }

  int _indexOf(String email) => _items.indexWhere(
    (p) => p.email.toLowerCase() == email.trim().toLowerCase(),
  );

  Future<void> _commit(List<SavedProfile> next) async {
    _items = next;
    notifyListeners();
    await _store.save(next);
  }
}

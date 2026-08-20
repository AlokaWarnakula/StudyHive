import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Thin key/value abstraction over secure storage so widget tests can inject an in-memory fake —
/// flutter_secure_storage talks to a real platform channel (Keychain/Keystore) that isn't present
/// under `flutter test`, so exercising it directly there hangs waiting for a response.
abstract class TokenStore {
  Future<String?> read(String key);
  Future<void> write(String key, String value);
  Future<void> delete(String key);
}

class SecureTokenStore implements TokenStore {
  final FlutterSecureStorage _storage;

  const SecureTokenStore([FlutterSecureStorage storage = const FlutterSecureStorage()]) : _storage = storage;

  @override
  Future<String?> read(String key) => _storage.read(key: key);

  @override
  Future<void> write(String key, String value) => _storage.write(key: key, value: value);

  @override
  Future<void> delete(String key) => _storage.delete(key: key);
}

import 'package:flutter/foundation.dart';

import '../api/api_client.dart';
import 'token_store.dart';

/// Thrown when a non-Student account tries to sign in — this app is Student-only
/// (DOCS §01/02: Librarian/StoreOfficer/Admin use the React dashboard).
class WrongRoleException implements Exception {
  final String message;
  WrongRoleException(this.message);

  @override
  String toString() => message;
}

const _accessTokenKey = 'access_token';
const _refreshTokenKey = 'refresh_token';

/// Session state for the Student role (ADR-2: Provider, least boilerplate). Both the access and
/// refresh tokens are persisted via [TokenStore] — the platform secure store (Keychain/Keystore)
/// in production, never plain SharedPreferences — so the app can restore a session across restarts.
class AuthProvider extends ChangeNotifier {
  final TokenStore _tokenStore;
  final ApiClient _apiClient;

  String? _accessToken;
  String? _refreshToken;
  String? _studentName;

  AuthProvider({TokenStore? tokenStore, ApiClient? apiClient})
      : _tokenStore = tokenStore ?? const SecureTokenStore(),
        _apiClient = apiClient ?? ApiClient();

  bool get isAuthenticated => _accessToken != null;
  String? get studentName => _studentName;

  Future<void> login(String email, String password) async {
    final response = await _apiClient.post('/api/auth/login', body: {
      'email': email,
      'password': password,
    }) as Map<String, dynamic>;

    final user = response['user'] as Map<String, dynamic>;
    if (user['role'] != 'Student') {
      throw WrongRoleException("This account can't sign in to the mobile app. Use the StudyHive staff dashboard instead.");
    }

    await _applySession(
      accessToken: response['accessToken'] as String,
      refreshToken: response['refreshToken'] as String,
      studentName: user['fullName'] as String,
    );
  }

  /// Attempts to restore a session from storage on app start by exchanging the stored refresh
  /// token for a fresh access token — never trusts a persisted access token blindly, since it may
  /// already have expired.
  Future<void> tryRestoreSession() async {
    final storedRefreshToken = await _tokenStore.read(_refreshTokenKey);
    if (storedRefreshToken == null) return;

    try {
      final response = await _apiClient.post('/api/auth/refresh', body: {
        'refreshToken': storedRefreshToken,
      }) as Map<String, dynamic>;

      final user = response['user'] as Map<String, dynamic>;
      await _applySession(
        accessToken: response['accessToken'] as String,
        refreshToken: response['refreshToken'] as String,
        studentName: user['fullName'] as String,
      );
    } on ApiException {
      // Expired/revoked — fall through to a clean logged-out state.
      await _clearSession();
    }
  }

  Future<void> logout() async {
    final refreshToken = _refreshToken;
    await _clearSession();
    if (refreshToken != null) {
      // Best-effort: an unreachable API shouldn't block the user from logging out locally.
      try {
        await _apiClient.post('/api/auth/logout', body: {'refreshToken': refreshToken});
      } catch (_) {
        // ignored
      }
    }
  }

  Future<void> _applySession({required String accessToken, required String refreshToken, required String studentName}) async {
    _accessToken = accessToken;
    _refreshToken = refreshToken;
    _studentName = studentName;
    _apiClient.accessToken = accessToken;
    await _tokenStore.write(_accessTokenKey, accessToken);
    await _tokenStore.write(_refreshTokenKey, refreshToken);
    notifyListeners();
  }

  Future<void> _clearSession() async {
    _accessToken = null;
    _refreshToken = null;
    _studentName = null;
    _apiClient.accessToken = null;
    await _tokenStore.delete(_accessTokenKey);
    await _tokenStore.delete(_refreshTokenKey);
    notifyListeners();
  }
}

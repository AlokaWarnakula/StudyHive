import 'dart:convert';

import 'package:http/http.dart' as http;

/// Base URL of the ASP.NET Core API. Override at build/run time with:
///   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5299
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://10.0.2.2:5299',
);

class ApiException implements Exception {
  final int status;
  final String? title;
  final String? detail;

  ApiException(this.status, this.title, this.detail);

  @override
  String toString() => detail ?? title ?? 'Request failed with status $status';
}

/// Thin http wrapper: JSON in/out, bearer auth, RFC7807 problem bodies surfaced as ApiException.
class ApiClient {
  final http.Client _client;
  String? accessToken;

  ApiClient({http.Client? client}) : _client = client ?? http.Client();

  Future<dynamic> get(String path) => _send('GET', path);

  Future<dynamic> post(String path, {Object? body}) => _send('POST', path, body: body);

  Future<dynamic> _send(String method, String path, {Object? body}) async {
    final uri = Uri.parse('$apiBaseUrl$path');
    final headers = {
      'Content-Type': 'application/json',
      if (accessToken != null) 'Authorization': 'Bearer $accessToken',
    };

    final request = http.Request(method, uri)..headers.addAll(headers);
    if (body != null) {
      request.body = jsonEncode(body);
    }

    final streamed = await _client.send(request);
    final response = await http.Response.fromStream(streamed);

    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return null;
      return jsonDecode(response.body);
    }

    Map<String, dynamic> problem = {};
    try {
      problem = jsonDecode(response.body) as Map<String, dynamic>;
    } catch (_) {
      // non-JSON error body, fall through with an empty problem map
    }
    throw ApiException(
      response.statusCode,
      problem['title'] as String?,
      problem['detail'] as String?,
    );
  }
}

import '../models/student_profile.dart';
import 'api_client.dart';

class StudentProfilesApi {
  final ApiClient _client;
  const StudentProfilesApi(this._client);

  /// Returns null when the signed-in student hasn't onboarded yet (server 404) rather than throwing —
  /// callers (ProfileProvider) treat "no profile" as a normal state, not an error.
  Future<StudentProfile?> getMine() async {
    try {
      final response =
          await _client.get('/api/student-profiles/me') as Map<String, dynamic>;
      return StudentProfile.fromJson(response);
    } on ApiException catch (e) {
      if (e.status == 404) return null;
      rethrow;
    }
  }

  Future<StudentProfile> create({
    required String studentNumber,
    required String department,
    required int yearOfStudy,
  }) async {
    final response = await _client.post('/api/student-profiles', body: {
      'studentNumber': studentNumber,
      'department': department,
      'yearOfStudy': yearOfStudy,
    }) as Map<String, dynamic>;
    return StudentProfile.fromJson(response);
  }
}

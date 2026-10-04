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

  /// PUT /api/student-profiles/me (PLAN.md 3.1a): the student's own name, number, department, year.
  Future<StudentProfile> updateMine({
    required String fullName,
    required String studentNumber,
    required String department,
    required int yearOfStudy,
  }) async {
    final response = await _client.put('/api/student-profiles/me', body: {
      'fullName': fullName,
      'studentNumber': studentNumber,
      'department': department,
      'yearOfStudy': yearOfStudy,
    }) as Map<String, dynamic>;
    return StudentProfile.fromJson(response);
  }

  /// GET /api/student-profiles/{id}/eligibility: the server's own weekly count (AUDIT C-16).
  Future<Eligibility> eligibility(String profileId) async {
    final response =
        await _client.get('/api/student-profiles/$profileId/eligibility')
            as Map<String, dynamic>;
    return Eligibility.fromJson(response);
  }
}

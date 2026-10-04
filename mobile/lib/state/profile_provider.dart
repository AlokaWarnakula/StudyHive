import 'package:flutter/foundation.dart';

import '../api/student_profiles_api.dart';
import '../models/student_profile.dart';

/// Onboarding + read state for the signed-in student's own profile (DOCS §11: self-service
/// onboarding). `profile == null` after a successful [refresh] means "not onboarded yet", not
/// an error — the UI shows the onboarding form in that case (see ProfileScreen).
class ProfileProvider extends ChangeNotifier {
  final StudentProfilesApi _api;
  ProfileProvider(this._api);

  StudentProfile? _profile;
  Eligibility? _eligibility;
  bool _loading = false;
  String? _error;
  bool _loaded = false;

  StudentProfile? get profile => _profile;

  /// The server's weekly count and verdict (AUDIT C-16); null until loaded or if it failed.
  Eligibility? get eligibility => _eligibility;
  bool get loading => _loading;
  String? get error => _error;
  bool get loaded => _loaded;

  Future<void> refresh() async {
    _loading = true;
    _error = null;
    notifyListeners();
    try {
      _profile = await _api.getMine();
      _loaded = true;
      final profile = _profile;
      if (profile != null) {
        try {
          _eligibility = await _api.eligibility(profile.id);
        } catch (_) {
          _eligibility = null; // the allowance tile falls back to the profile's limit
        }
      }
    } catch (e) {
      _error = e.toString();
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<void> onboard(
      {required String studentNumber,
      required String department,
      required int yearOfStudy}) async {
    _profile = await _api.create(
        studentNumber: studentNumber,
        department: department,
        yearOfStudy: yearOfStudy);
    notifyListeners();
  }

  /// PLAN.md 3.2: save the edited profile; the caller updates the signed-in name.
  Future<StudentProfile> update({
    required String fullName,
    required String studentNumber,
    required String department,
    required int yearOfStudy,
  }) async {
    final updated = await _api.updateMine(
        fullName: fullName,
        studentNumber: studentNumber,
        department: department,
        yearOfStudy: yearOfStudy);
    _profile = updated;
    notifyListeners();
    return updated;
  }

  void reset() {
    _profile = null;
    _eligibility = null;
    _loaded = false;
    _error = null;
    notifyListeners();
  }
}

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
  bool _loading = false;
  String? _error;
  bool _loaded = false;

  StudentProfile? get profile => _profile;
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

  void reset() {
    _profile = null;
    _loaded = false;
    _error = null;
    notifyListeners();
  }
}

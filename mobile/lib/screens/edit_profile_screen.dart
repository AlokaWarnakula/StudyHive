import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../state/auth_provider.dart';
import '../state/profile_provider.dart';
import '../widgets/studyhive_ui.dart';

/// PLAN.md 3.2 "Edit profile" — PUT /api/student-profiles/me. Full name, student number,
/// department and year; email, weekly limit, penalties and suspension stay read-only.
/// A server error lands on the field it belongs to (409 → student number).
class EditProfileScreen extends StatefulWidget {
  const EditProfileScreen({super.key});

  @override
  State<EditProfileScreen> createState() => _EditProfileScreenState();
}

const _fields = {
  'fullname': 'fullName',
  'studentnumber': 'studentNumber',
  'department': 'department',
  'yearofstudy': 'yearOfStudy',
};

class _EditProfileScreenState extends State<EditProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _name;
  late final TextEditingController _number;
  late final TextEditingController _department;
  late final TextEditingController _year;
  final Map<String, String> _serverErrors = {};
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final profile = context.read<ProfileProvider>().profile!;
    _name = TextEditingController(
        text: profile.fullName ?? context.read<AuthProvider>().studentName ?? '');
    _number = TextEditingController(text: profile.studentNumber);
    _department = TextEditingController(text: profile.department);
    _year = TextEditingController(text: '${profile.yearOfStudy}');
  }

  @override
  void dispose() {
    _name.dispose();
    _number.dispose();
    _department.dispose();
    _year.dispose();
    super.dispose();
  }

  String? Function(String?) _required(String field, String message, {int? max}) =>
      (v) {
        if (_serverErrors[field] != null) return _serverErrors[field];
        final value = v?.trim() ?? '';
        if (value.isEmpty) return message;
        if (max != null && value.length > max) return 'Use at most $max characters';
        return null;
      };

  Future<void> _save() async {
    _serverErrors.clear();
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _error = null;
      _saving = true;
    });
    final auth = context.read<AuthProvider>();
    try {
      final updated = await context.read<ProfileProvider>().update(
            fullName: _name.text.trim(),
            studentNumber: _number.text.trim(),
            department: _department.text.trim(),
            yearOfStudy: int.parse(_year.text.trim()),
          );
      auth.updateStudentName(updated.fullName ?? _name.text.trim());
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Profile saved')));
      Navigator.of(context).pop();
    } on ApiException catch (e) {
      setState(() {
        if (e.status == 409) {
          _serverErrors['studentNumber'] = 'This student number is already registered';
        } else {
          // ASP.NET names fields "FullName", "StudentNumber"…; match them case-insensitively.
          for (final entry in e.errors.entries) {
            final field = _fields[entry.key.toLowerCase()];
            if (field != null && entry.value.isNotEmpty) _serverErrors[field] = entry.value.first;
          }
          if (_serverErrors.isEmpty) _error = e.toString();
        }
      });
      _formKey.currentState!.validate();
    } catch (_) {
      setState(() => _error = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Edit profile')),
      body: Form(
        key: _formKey,
        child: ScreenBody(
          children: [
            ShTextField(
              label: 'Full name',
              controller: _name,
              validator: _required('fullName', 'Full name is required', max: 150),
            ),
            ShTextField(
              label: 'Student number',
              controller: _number,
              validator: _required('studentNumber', 'Student number is required', max: 20),
            ),
            ShTextField(
              label: 'Department',
              controller: _department,
              validator: _required('department', 'Department is required', max: 80),
            ),
            ShTextField(
              label: 'Year of study',
              controller: _year,
              keyboardType: TextInputType.number,
              validator: (v) {
                if (_serverErrors['yearOfStudy'] != null) return _serverErrors['yearOfStudy'];
                final n = int.tryParse(v?.trim() ?? '');
                return n == null || n < 1 || n > 5 ? 'Enter a year between 1 and 5' : null;
              },
            ),
            const FNote(
              'Your email, weekly limit and penalties are managed by the library.',
            ),
            if (_error != null) InlineError(_error!),
            PrimaryButton(
              _saving ? 'Saving…' : 'Save changes',
              onPressed: _saving ? null : _save,
            ),
          ],
        ),
      ),
    );
  }
}

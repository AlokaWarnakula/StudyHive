import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../state/auth_provider.dart';
import '../widgets/studyhive_ui.dart';

/// PLAN.md 3.2 "Change password" — POST /api/auth/change-password. The API signs out every other
/// device and returns a fresh token pair, which AuthProvider stores so this phone stays signed in.
class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _current = TextEditingController();
  final _new = TextEditingController();
  final _confirm = TextEditingController();
  String? _currentError;
  String? _newError;
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _current.dispose();
    _new.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    _currentError = null;
    _newError = null;
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _error = null;
      _saving = true;
    });
    try {
      await context.read<AuthProvider>().changePassword(
            currentPassword: _current.text,
            newPassword: _new.text,
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Password changed. Other devices have been signed out.')));
      Navigator.of(context).pop();
    } on ApiException catch (e) {
      setState(() {
        String? first(String key) {
          for (final entry in e.errors.entries) {
            if (entry.key.toLowerCase() == key && entry.value.isNotEmpty) return entry.value.first;
          }
          return null;
        }

        _currentError = first('currentpassword');
        _newError = first('newpassword');
        if (_currentError == null && _newError == null) _error = e.toString();
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
      appBar: AppBar(title: const Text('Change password')),
      body: Form(
        key: _formKey,
        child: ScreenBody(
          children: [
            ShTextField(
              label: 'Current password',
              controller: _current,
              obscureText: true,
              autofillHints: const [AutofillHints.password],
              validator: (v) => _currentError ??
                  ((v == null || v.isEmpty) ? 'Enter your current password' : null),
            ),
            ShTextField(
              label: 'New password',
              controller: _new,
              obscureText: true,
              autofillHints: const [AutofillHints.newPassword],
              validator: (v) {
                if (_newError != null) return _newError;
                final value = v ?? '';
                if (value.length < 8) return 'Use at least 8 characters';
                if (value.length > 100) return 'Use at most 100 characters';
                if (value == _current.text) return 'Choose a different password';
                return null;
              },
            ),
            ShTextField(
              label: 'Confirm new password',
              controller: _confirm,
              obscureText: true,
              autofillHints: const [AutofillHints.newPassword],
              validator: (v) => v != _new.text ? 'Passwords do not match' : null,
            ),
            const FNote('Changing it signs you out on your other devices.'),
            if (_error != null) InlineError(_error!),
            PrimaryButton(
              _saving ? 'Saving…' : 'Change password',
              onPressed: _saving ? null : _save,
            ),
          ],
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import 'dialog_header.dart';

/// Asks to confirm a cancellation and for the reason. Returns the trimmed reason (empty when it was optional and
/// left blank), or null when the person backed out. The salon must always give a reason ([reasonRequired]);
/// a client may cancel without one.
Future<String?> showCancelReasonDialog(
  BuildContext context, {
  required String summary,
  required bool reasonRequired,
}) => showDialog<String>(
  context: context,
  builder: (_) =>
      _CancelReasonDialog(summary: summary, reasonRequired: reasonRequired),
);

// Owns its controller, so it is disposed only after the dialog is really gone.
class _CancelReasonDialog extends StatefulWidget {
  const _CancelReasonDialog({
    required this.summary,
    required this.reasonRequired,
  });

  final String summary;
  final bool reasonRequired;

  @override
  State<_CancelReasonDialog> createState() => _CancelReasonDialogState();
}

class _CancelReasonDialogState extends State<_CancelReasonDialog> {
  static const _min = 3;
  static const _max = 300;

  final _form = GlobalKey<FormState>();
  final _reason = TextEditingController();

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  void _confirm() {
    if (!_form.currentState!.validate()) return;
    Navigator.pop(context, _reason.text.trim());
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AlertDialog(
      title: DialogHeader(t.cancelBooking),
      content: SizedBox(
        width: 420,
        child: Form(
          key: _form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (widget.summary.isNotEmpty) ...[
                Text(widget.summary),
                const SizedBox(height: 8),
              ],
              Text(t.cancelBookingQuestion),
              const SizedBox(height: 16),
              TextFormField(
                key: const Key('cancel_reason'),
                controller: _reason,
                maxLines: 2,
                maxLength: _max,
                decoration: InputDecoration(
                  labelText: widget.reasonRequired
                      ? t.cancelReasonLabel
                      : t.cancelReasonOptionalLabel,
                ),
                validator: (v) {
                  final text = (v ?? '').trim();
                  if (widget.reasonRequired && text.length < _min) {
                    return t.cancelReasonRequired;
                  }
                  return null;
                },
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(t.no)),
        FilledButton(
          key: const Key('confirm_cancel'),
          style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
          onPressed: _confirm,
          child: Text(t.yes),
        ),
      ],
    );
  }
}

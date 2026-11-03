import 'package:flutter/material.dart';

import '../l10n/app_localizations.dart';
import 'dialog_header.dart';

/// Asks before an action that cannot be taken back (delete, hand over, send to many people).
/// Completes with true only when the user pressed [confirmLabel]; closing the dialog any other way is a no.
Future<bool> confirmAction(
  BuildContext context, {
  required String title,
  required String message,
  required String confirmLabel,
  Key confirmKey = const Key('confirm_action'),
}) async {
  final t = AppLocalizations.of(context);
  final ok = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: DialogHeader(title),
      content: Text(message, key: const Key('confirm_action_text')),
      actions: [
        TextButton(
          key: const Key('confirm_action_cancel'),
          onPressed: () => Navigator.pop(ctx, false),
          child: Text(t.cancel),
        ),
        FilledButton(
          key: confirmKey,
          onPressed: () => Navigator.pop(ctx, true),
          child: Text(confirmLabel),
        ),
      ],
    ),
  );
  return ok == true;
}

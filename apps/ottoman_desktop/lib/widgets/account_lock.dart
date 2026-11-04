import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// "Locked until 11:15" for an account that too many wrong passwords keep from signing in. A lock lasts minutes,
/// so the time of day is enough; the full date is in the tooltip.
class LockedChip extends StatelessWidget {
  const LockedChip({super.key, required this.until});

  final DateTime until;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final local = until.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    // A plain pill rather than a Chip: it measures its own width exactly, so it sits in a table cell without overflow.
    return Tooltip(
      message: t.lockedUntil(formatWhen(until)),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: WorkspaceColors.danger.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.lock_outline,
              size: 14,
              color: WorkspaceColors.danger,
            ),
            const SizedBox(width: 6),
            Text(
              t.lockedUntil('${two(local.hour)}:${two(local.minute)}'),
              style: const TextStyle(
                color: WorkspaceColors.danger,
                fontWeight: FontWeight.w700,
                fontSize: 12,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Asks first, then ends the sign-in lock and says so. True when the account was unlocked (the caller refreshes).
Future<bool> unlockAccount(
  BuildContext context, {
  required String name,
  required Future<void> Function() unlock,
}) async {
  final t = AppLocalizations.of(context);
  final messenger = ScaffoldMessenger.of(context);
  final ok = await confirmAction(
    context,
    title: t.unlockAccount,
    message: t.unlockAccountQuestion(name),
    confirmLabel: t.unlockAccount,
  );
  if (!ok) return false;
  try {
    await unlock();
    messenger.showSnackBar(SnackBar(content: Text(t.accountUnlocked)));
    return true;
  } on AppException catch (e) {
    messenger.showSnackBar(SnackBar(content: Text(e.message)));
    return false;
  }
}

/// The button that starts [unlockAccount]; shown only while the account is locked.
class UnlockButton extends StatelessWidget {
  const UnlockButton({super.key, required this.onPressed});

  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) => TextButton.icon(
    style: TextButton.styleFrom(
      visualDensity: VisualDensity.compact,
      padding: const EdgeInsets.symmetric(horizontal: 8),
      minimumSize: Size.zero,
      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
    ),
    onPressed: onPressed,
    icon: const Icon(Icons.lock_open_outlined, size: 18),
    label: Text(AppLocalizations.of(context).unlockAccount),
  );
}

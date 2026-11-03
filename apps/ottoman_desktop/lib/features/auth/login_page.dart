import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

const _stage = Color(0xFF141414);
const _quiet = Color(0xFF8C8C8C);

/// "Who is signing in?" - the accounts that signed in on this computer before, as cards (like a streaming service's
/// profile picker). A card asks only for the password; a new account is added with e-mail and password and then
/// shows up as a card. The pencil below lets cards be removed again. Only e-mail, name and photo are remembered,
/// never a password.
class LoginPage extends StatefulWidget {
  const LoginPage({super.key, required this.auth, required this.profiles});

  final AuthController auth;
  final SavedProfiles profiles;

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  SavedProfile? _chosen;
  bool _adding = false;
  bool _editing = false;

  void _back() => setState(() {
    _chosen = null;
    _adding = false;
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: _stage,
      body: ListenableBuilder(
        listenable: widget.profiles,
        builder: (context, _) {
          final items = widget.profiles.items;
          // Nothing remembered yet: go straight to the form for a first account.
          final adding = _adding || (items.isEmpty && _chosen == null);
          final chosen = _chosen;
          final Widget body;
          if (chosen != null) {
            body = _PasswordStep(
              key: ValueKey('pw_${chosen.email}'),
              auth: widget.auth,
              profile: chosen,
              onBack: _back,
              onSignedIn: (u) => widget.profiles.remember(u),
            );
          } else if (adding) {
            body = _AddStep(
              auth: widget.auth,
              onBack: items.isEmpty ? null : _back,
              onSignedIn: (u) => widget.profiles.remember(u),
            );
          } else {
            body = _PickerStep(
              profiles: widget.profiles,
              editing: _editing,
              onToggleEditing: () => setState(() => _editing = !_editing),
              onChoose: (p) => setState(() => _chosen = p),
              onAdd: () => setState(() => _adding = true),
            );
          }
          return Stack(
            children: [
              const Positioned(
                left: 32,
                top: 24,
                child: OttomanLogo(
                  key: Key('login_logo'),
                  variant: LogoVariant.horizontal,
                  light: true,
                  height: 46,
                ),
              ),
              Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.fromLTRB(32, 96, 32, 48),
                  child: body,
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

// ---- step 1: the cards

class _PickerStep extends StatelessWidget {
  const _PickerStep({
    required this.profiles,
    required this.editing,
    required this.onToggleEditing,
    required this.onChoose,
    required this.onAdd,
  });

  final SavedProfiles profiles;
  final bool editing;
  final VoidCallback onToggleEditing;
  final ValueChanged<SavedProfile> onChoose;
  final VoidCallback onAdd;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 1000),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            editing ? t.manageAccounts : t.chooseAccount,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 40,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: 44),
          Wrap(
            alignment: WrapAlignment.center,
            spacing: 32,
            runSpacing: 32,
            children: [
              for (final p in profiles.items)
                _ProfileCard(
                  profile: p,
                  editing: editing,
                  onTap: editing ? null : () => onChoose(p),
                  onRemove: () => profiles.remove(p.email),
                ),
              if (!editing) _AddCard(label: t.addAccount, onTap: onAdd),
            ],
          ),
          const SizedBox(height: 56),
          if (editing)
            FilledButton(
              key: const Key('profiles_manage'),
              style: FilledButton.styleFrom(
                backgroundColor: Colors.white,
                foregroundColor: Colors.black,
                minimumSize: const Size(140, 46),
              ),
              onPressed: onToggleEditing,
              child: Text(t.done),
            )
          else
            Tooltip(
              message: t.manageAccounts,
              child: InkResponse(
                key: const Key('profiles_manage'),
                onTap: onToggleEditing,
                radius: 28,
                child: Container(
                  width: 46,
                  height: 46,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    border: Border.all(color: _quiet, width: 2),
                  ),
                  child: const Icon(Icons.edit, color: _quiet, size: 22),
                ),
              ),
            ),
          if (editing) ...[
            const SizedBox(height: 16),
            Text(
              t.removeAccountHint,
              style: const TextStyle(color: _quiet, fontSize: 13),
            ),
          ],
        ],
      ),
    );
  }
}

const _cardSize = 150.0;

class _ProfileCard extends StatefulWidget {
  const _ProfileCard({
    required this.profile,
    required this.editing,
    required this.onTap,
    required this.onRemove,
  });

  final SavedProfile profile;
  final bool editing;
  final VoidCallback? onTap;
  final VoidCallback onRemove;

  @override
  State<_ProfileCard> createState() => _ProfileCardState();
}

class _ProfileCardState extends State<_ProfileCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final p = widget.profile;
    final lit = _hover && !widget.editing;
    return MouseRegion(
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      cursor: widget.onTap == null
          ? SystemMouseCursors.basic
          : SystemMouseCursors.click,
      child: SizedBox(
        width: _cardSize + 12,
        child: Column(
          children: [
            GestureDetector(
              key: Key('profile_${p.email}'),
              onTap: widget.onTap,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 120),
                padding: const EdgeInsets.all(3),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(_cardSize * 0.1),
                  border: Border.all(
                    color: lit ? Colors.white : Colors.transparent,
                    width: 3,
                  ),
                ),
                child: Stack(
                  alignment: Alignment.center,
                  children: [
                    PersonAvatarTile(
                      photoUrl: p.photoUrl,
                      gender: p.gender,
                      seed: p.avatarSeed,
                      size: _cardSize,
                    ),
                    if (widget.editing)
                      Positioned.fill(
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(_cardSize * 0.08),
                          child: ColoredBox(
                            color: Colors.black54,
                            child: Center(
                              child: Tooltip(
                                message: t.removeFromList,
                                child: IconButton(
                                  key: Key('profile_remove_${p.email}'),
                                  iconSize: 40,
                                  color: Colors.white,
                                  onPressed: widget.onRemove,
                                  icon: const Icon(Icons.delete_outline),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 10),
            Text(
              p.name,
              maxLines: 2,
              textAlign: TextAlign.center,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: lit ? Colors.white : _quiet,
                fontSize: 17,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AddCard extends StatefulWidget {
  const _AddCard({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  State<_AddCard> createState() => _AddCardState();
}

class _AddCardState extends State<_AddCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final lit = _hover;
    return MouseRegion(
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      cursor: SystemMouseCursors.click,
      child: SizedBox(
        width: _cardSize + 12,
        child: Column(
          children: [
            GestureDetector(
              key: const Key('profile_add'),
              onTap: widget.onTap,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 120),
                margin: const EdgeInsets.all(3),
                width: _cardSize + 6,
                height: _cardSize + 6,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(_cardSize * 0.1),
                  border: Border.all(
                    color: lit ? Colors.white : Colors.transparent,
                    width: 3,
                  ),
                ),
                child: Center(
                  child: Icon(
                    Icons.add_circle,
                    size: _cardSize * 0.6,
                    color: lit ? Colors.white : _quiet,
                  ),
                ),
              ),
            ),
            const SizedBox(height: 10),
            Text(
              widget.label,
              textAlign: TextAlign.center,
              style: TextStyle(
                color: lit ? Colors.white : _quiet,
                fontSize: 17,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ---- step 2a: password for a chosen card

class _PasswordStep extends StatelessWidget {
  const _PasswordStep({
    super.key,
    required this.auth,
    required this.profile,
    required this.onBack,
    required this.onSignedIn,
  });

  final AuthController auth;
  final SavedProfile profile;
  final VoidCallback onBack;
  final void Function(AppUser user) onSignedIn;

  @override
  Widget build(BuildContext context) {
    return _FormCard(
      onBack: onBack,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Center(
            child: PersonAvatarTile(
              photoUrl: profile.photoUrl,
              gender: profile.gender,
              seed: profile.avatarSeed,
              size: 104,
            ),
          ),
          const SizedBox(height: 16),
          Text(
            profile.name,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.headlineSmall
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
          Text(
            profile.email,
            textAlign: TextAlign.center,
            style: const TextStyle(color: WorkspaceColors.muted),
          ),
          const SizedBox(height: 24),
          LoginForm(
            auth: auth,
            initialEmail: profile.email,
            hideEmail: true,
            onSignedIn: onSignedIn,
          ),
        ],
      ),
    );
  }
}

// ---- step 2b: a new account

class _AddStep extends StatelessWidget {
  const _AddStep({
    required this.auth,
    required this.onBack,
    required this.onSignedIn,
  });

  final AuthController auth;
  final VoidCallback? onBack;
  final void Function(AppUser user) onSignedIn;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return _FormCard(
      onBack: onBack,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            t.welcomeBack,
            style: Theme.of(context).textTheme.headlineSmall
                ?.copyWith(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          Text(
            t.signInSubtitle,
            style: const TextStyle(color: WorkspaceColors.muted),
          ),
          const SizedBox(height: 24),
          LoginForm(auth: auth, onSignedIn: onSignedIn),
        ],
      ),
    );
  }
}

/// White panel on the dark page, with an optional way back to the cards.
class _FormCard extends StatelessWidget {
  const _FormCard({required this.child, required this.onBack});

  final Widget child;
  final VoidCallback? onBack;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 420),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (onBack != null)
            TextButton.icon(
              key: const Key('login_back'),
              onPressed: onBack,
              style: TextButton.styleFrom(foregroundColor: Colors.white),
              icon: const Icon(Icons.arrow_back, size: 18),
              label: Text(t.backToAccounts),
            ),
          const SizedBox(height: 8),
          Card(
            child: Padding(padding: const EdgeInsets.all(28), child: child),
          ),
        ],
      ),
    );
  }
}

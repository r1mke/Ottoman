import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../images/picture_editor.dart';

/// A barber's own picture: upload or remove it. Sent to the server at once (like the admin's picture editor);
/// the crop around the face is the server's default, the barber does not set it.
class OwnPhotoSection extends StatelessWidget {
  const OwnPhotoSection({
    super.key,
    required this.auth,
    this.chooser = const FileImageChooser(),
  });

  final AuthController auth;
  final ImageChooser chooser;

  static List<EditablePicture> _pictures(String? url) => [
    if (url != null) EditablePicture('photo', url),
  ];

  Future<List<EditablePicture>> _afterChange() async {
    await auth.reloadUser();
    return _pictures(auth.user!.photoUrl);
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: auth,
    builder: (context, _) {
      final user = auth.user!;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          PictureEditor(
            // A new key per photo, so the editor starts from what the server now has.
            key: ValueKey('own_photo_${user.photoUrl}'),
            chooser: chooser,
            fallbackIcon: Icons.person_outline,
            pictures: _pictures(user.photoUrl),
            onAdd: (img) async {
              await auth.api.put<dynamic>(
                '/api/v1/me/photo',
                body: imageForm(img.bytes, img.name),
              );
              return _afterChange();
            },
            onRemove: (_) async {
              await auth.api.delete<dynamic>('/api/v1/me/photo');
              return _afterChange();
            },
          ),
        ],
      );
    },
  );
}

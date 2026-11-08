// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appName => 'OTTOMAN';

  @override
  String get login => 'Sign in';

  @override
  String get logout => 'Sign out';

  @override
  String get email => 'Email';

  @override
  String get password => 'Password';

  @override
  String get retry => 'Try again';

  @override
  String get cancel => 'Cancel';

  @override
  String get save => 'Save';

  @override
  String get edit => 'Edit';

  @override
  String get delete => 'Delete';

  @override
  String get employees => 'Staff';

  @override
  String get services => 'Services';

  @override
  String get appointments => 'Appointments';

  @override
  String get products => 'Products';

  @override
  String get settings => 'Settings';

  @override
  String get home => 'Home';

  @override
  String get noData => 'No data';

  @override
  String get currencyShort => 'BAM';

  @override
  String get register => 'Sign up';

  @override
  String get createAccount => 'Create account';

  @override
  String get haveAccount => 'Already have an account? Sign in';

  @override
  String get noAccount => 'No account? Sign up';

  @override
  String get forgotPassword => 'Forgot password?';

  @override
  String get resetEmailSent =>
      'If the account exists, we\'ve emailed instructions.';

  @override
  String get firstName => 'First name';

  @override
  String get lastName => 'Last name';

  @override
  String get phone => 'Phone';

  @override
  String get profile => 'Profile';

  @override
  String get saveChanges => 'Save changes';

  @override
  String get profileSaved => 'Profile saved.';

  @override
  String get requiredField => 'Required';

  @override
  String get invalidEmail => 'Enter an email in the format name@domain.com';

  @override
  String get invalidPhone =>
      'Enter a valid phone number, e.g. 061 123 456 or +387 61 123 456.';

  @override
  String get invalidName =>
      'Enter 2 to 50 letters (spaces, dots, apostrophes and hyphens are allowed).';

  @override
  String get invalidPrice =>
      'Enter a price above 0 with at most two decimals (e.g. 12.50).';

  @override
  String invalidWholeNumber(int min, int max) {
    return 'Enter a whole number from $min to $max.';
  }

  @override
  String get passwordRules =>
      'At least 8 characters, upper and lower case, a digit and a symbol.';

  @override
  String get registrationDone => 'Account created. You can sign in now.';

  @override
  String get welcomeBack => 'Welcome back';

  @override
  String get signInSubtitle => 'Sign in to continue';

  @override
  String get language => 'Language';

  @override
  String get overview => 'Overview';

  @override
  String get clients => 'Clients';

  @override
  String get notifications => 'Notifications';

  @override
  String get reports => 'Reports';

  @override
  String get earnings => 'Earnings';

  @override
  String get myProfile => 'My profile';

  @override
  String get comingSoon => 'Coming soon';

  @override
  String signedInAs(String name) {
    return 'Signed in as $name';
  }

  @override
  String get genericError => 'Something went wrong.';

  @override
  String get chooseEmployee => 'Choose a barber';

  @override
  String get chooseEmployeeHint =>
      'Barbers who do all of the chosen services are shown.';

  @override
  String get pricelist => 'Services & Prices';

  @override
  String minutesShort(int count) {
    return '$count min';
  }

  @override
  String get total => 'Total';

  @override
  String get continueLabel => 'Continue';

  @override
  String get selectServices => 'Select one or more services';

  @override
  String get add => 'Add';

  @override
  String get name => 'Name';

  @override
  String get description => 'Description';

  @override
  String get price => 'Price';

  @override
  String get duration => 'Duration (min)';

  @override
  String get active => 'Active';

  @override
  String get inactive => 'Inactive';

  @override
  String get title => 'Title';

  @override
  String get bio => 'Bio';

  @override
  String get workingHours => 'Working hours';

  @override
  String get dayOff => 'Day off';

  @override
  String get breakLabel => 'Break';

  @override
  String get from => 'From';

  @override
  String get to => 'To';

  @override
  String get deactivate => 'Deactivate';

  @override
  String get searchHint => 'Search…';

  @override
  String get durationStepError => 'Duration must be a multiple of 5 minutes.';

  @override
  String get invalidNumber => 'Enter a valid number';

  @override
  String get book => 'Book';

  @override
  String get noSlots => 'No available times for the selected date';

  @override
  String get showAllTimes => 'Show all times';

  @override
  String get showFewerTimes => 'Show fewer times';

  @override
  String get morning => 'Morning';

  @override
  String get afternoon => 'Afternoon';

  @override
  String get evening => 'Evening';

  @override
  String get note => 'Note';

  @override
  String get noteHint => 'Leave a message';

  @override
  String get bookingConfirmed => 'Appointment booked.';

  @override
  String get upcoming => 'Upcoming';

  @override
  String get past => 'Past';

  @override
  String get cancelBooking => 'Cancel appointment';

  @override
  String get cancelBookingQuestion =>
      'Are you sure you want to cancel this appointment?';

  @override
  String get no => 'No';

  @override
  String get yes => 'Yes';

  @override
  String get statusPending => 'Awaiting confirmation';

  @override
  String get statusConfirmed => 'Confirmed';

  @override
  String get statusCompleted => 'Completed';

  @override
  String get statusCancelled => 'Cancelled';

  @override
  String get statusNoShow => 'No-show';

  @override
  String get complete => 'Complete';

  @override
  String get markNoShow => 'No-show';

  @override
  String get reschedule => 'Edit appointment';

  @override
  String get newAppointment => 'New appointment';

  @override
  String get client => 'Client';

  @override
  String get employee => 'Barber';

  @override
  String get date => 'Date';

  @override
  String get time => 'Time';

  @override
  String get service => 'Service';

  @override
  String get status => 'Status';

  @override
  String get allEmployees => 'All barbers';

  @override
  String get today => 'Today';

  @override
  String get weekly => 'Weekly';

  @override
  String get monthly => 'Monthly';

  @override
  String get statUpcoming => 'Upcoming';

  @override
  String get statCompleted => 'Completed';

  @override
  String get statCancelled => 'Cancelled';

  @override
  String get statClients => 'Total clients';

  @override
  String get appointmentsOverview => 'Appointments';

  @override
  String get noAppointments => 'No appointments';

  @override
  String get chooseClient => 'Choose a client';

  @override
  String get searchClient => 'Search clients';

  @override
  String get previousDay => 'Previous day';

  @override
  String get nextDay => 'Next day';

  @override
  String get slotTaken => 'That time was just taken. Please pick another.';

  @override
  String get cart => 'Cart';

  @override
  String get addToCart => 'Add to cart';

  @override
  String get outOfStock => 'Out of stock';

  @override
  String get lowStock => 'Low stock';

  @override
  String get stock => 'Stock';

  @override
  String inStockCount(int count) {
    return 'In stock: $count';
  }

  @override
  String get allCategories => 'All categories';

  @override
  String get sortBy => 'Sort';

  @override
  String get sortName => 'Name';

  @override
  String get sortPriceAsc => 'Price: low to high';

  @override
  String get sortPriceDesc => 'Price: high to low';

  @override
  String get sortNewest => 'Newest';

  @override
  String get cartEmpty => 'Your cart is empty';

  @override
  String get limitReached => 'No more pieces in stock.';

  @override
  String get checkout => 'Checkout';

  @override
  String get pickup => 'Pick up at the salon';

  @override
  String get payByCard => 'By card';

  @override
  String get payOnReceipt => 'On receipt';

  @override
  String get placeOrder => 'Place order';

  @override
  String get orderPlaced => 'Your order has been received.';

  @override
  String get orderAwaitsPayment => 'The order is waiting for payment.';

  @override
  String get cardUnavailable =>
      'Card payment is not available in this app yet. Choose to pay on receipt.';

  @override
  String get payNow => 'Pay now';

  @override
  String get myOrders => 'My orders';

  @override
  String get noOrders => 'No orders';

  @override
  String get orders => 'Orders';

  @override
  String orderFrom(String date) {
    return 'Order of $date';
  }

  @override
  String get setPasswordTitle => 'Change password';

  @override
  String setPasswordHint(String name) {
    return 'New password for $name. The old password is not needed; the user is signed out everywhere.';
  }

  @override
  String get setPasswordDone =>
      'Password changed. The user has to sign in again.';

  @override
  String orderCancelReason(String reason) {
    return 'Reason for cancelling: $reason';
  }

  @override
  String get orderStatusPendingPayment => 'Awaiting payment';

  @override
  String get orderStatusPlaced => 'Received';

  @override
  String get orderStatusFulfilled => 'Picked up';

  @override
  String get orderStatusCancelled => 'Cancelled';

  @override
  String get orderStatusExpired => 'Expired';

  @override
  String get cancelOrder => 'Cancel order';

  @override
  String get cancelOrderQuestion => 'Do you really want to cancel this order?';

  @override
  String get markFulfilled => 'Picked up / delivered';

  @override
  String get restock => 'Adjust stock';

  @override
  String get stockChange => 'Stock change (e.g. 5 or -2)';

  @override
  String get brand => 'Brand';

  @override
  String get brands => 'Brands';

  @override
  String get category => 'Category';

  @override
  String get categories => 'Categories';

  @override
  String get lowStockOnly => 'Low stock only';

  @override
  String get newProduct => 'New product';

  @override
  String get lowStockThreshold => 'Low stock threshold';

  @override
  String get initialStock => 'Initial stock';

  @override
  String get manage => 'Manage';

  @override
  String get allStatuses => 'All statuses';

  @override
  String productsCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count products',
      one: '$count product',
    );
    return '$_temp0';
  }

  @override
  String get payment => 'Payment';

  @override
  String get paymentStatusPending => 'Awaiting payment';

  @override
  String get paymentStatusSucceeded => 'Paid';

  @override
  String get paymentStatusFailed => 'Failed';

  @override
  String get paymentStatusRefunded => 'Refunded';

  @override
  String get paymentStatusRefundPending => 'Refund pending';

  @override
  String get paymentNotConfirmed =>
      'The payment was not confirmed. The order waits for payment in \"My orders\"; if it stays unpaid, it closes on its own.';

  @override
  String get paymentSucceeded => 'Payment successful. Thank you!';

  @override
  String get paymentCancelledPayLater =>
      'Payment cancelled. You can pay for the order later.';

  @override
  String get none => 'None';

  @override
  String get noNotifications => 'No notifications';

  @override
  String get markAllRead => 'Mark all as read';

  @override
  String get audienceAll => 'Everyone';

  @override
  String get audienceClients => 'Clients';

  @override
  String get audienceEmployees => 'Employees';

  @override
  String get audienceUser => 'One user';

  @override
  String get audience => 'Audience';

  @override
  String get messageTitle => 'Title';

  @override
  String get messageBody => 'Message';

  @override
  String get alsoSendEmail => 'Also send by email';

  @override
  String get send => 'Send';

  @override
  String get announcementSent => 'Message sent.';

  @override
  String get sentMessages => 'Sent messages';

  @override
  String recipientsCount(int count) {
    return '$count recipients';
  }

  @override
  String get newMessage => 'New message';

  @override
  String get haveCode => 'I have a code';

  @override
  String get resetCode => 'Code from the email';

  @override
  String get newPassword => 'New password';

  @override
  String get passwordChanged => 'Password changed. Please sign in.';

  @override
  String get resetFailed => 'The code is wrong or has expired.';

  @override
  String get close => 'Close';

  @override
  String get daySunday => 'Sunday';

  @override
  String get dayMonday => 'Monday';

  @override
  String get dayTuesday => 'Tuesday';

  @override
  String get dayWednesday => 'Wednesday';

  @override
  String get dayThursday => 'Thursday';

  @override
  String get dayFriday => 'Friday';

  @override
  String get daySaturday => 'Saturday';

  @override
  String get reviews => 'Reviews';

  @override
  String get noReviews => 'No reviews yet';

  @override
  String get writeReview => 'Rate';

  @override
  String get yourRating => 'Your rating';

  @override
  String get yourComment => 'Comment (optional)';

  @override
  String get reviewSaved => 'Thank you for your review!';

  @override
  String get pickRating => 'Pick a rating.';

  @override
  String get recommendedForYou => 'You might like';

  @override
  String get similarProducts => 'Similar';

  @override
  String get hideReview => 'Hide';

  @override
  String get showReview => 'Show';

  @override
  String get reply => 'Reply';

  @override
  String get salonReply => 'Salon reply';

  @override
  String get hiddenReviews => 'Hidden only';

  @override
  String get replySaved => 'Reply saved.';

  @override
  String get yourReply => 'Your reply';

  @override
  String get periodToday => 'Today';

  @override
  String get period7 => '7 days';

  @override
  String get period30 => '30 days';

  @override
  String get periodThisMonth => 'This month';

  @override
  String get periodLastMonth => 'Last month';

  @override
  String get periodThisYear => 'This year';

  @override
  String get periodCustom => 'Pick a period';

  @override
  String get kpiRevenue => 'Total revenue';

  @override
  String get kpiServices => 'Services revenue';

  @override
  String get kpiProducts => 'Products revenue';

  @override
  String get kpiAppointments => 'Completed appointments';

  @override
  String get kpiOrders => 'Orders';

  @override
  String get kpiNewClients => 'New clients';

  @override
  String get kpiAverageTicket => 'Average ticket';

  @override
  String get kpiNoShow => 'No-show';

  @override
  String get kpiCancelled => 'Cancelled';

  @override
  String get vsPrevious => 'vs previous period';

  @override
  String get revenueOverTime => 'Revenue over time';

  @override
  String get topServices => 'Top services';

  @override
  String get topProducts => 'Best-selling products';

  @override
  String get employeePerformance => 'Employee performance';

  @override
  String get completedShort => 'Completed';

  @override
  String get noShowShort => 'No-show';

  @override
  String get cancelledShort => 'Cancelled';

  @override
  String get revenueShort => 'Revenue';

  @override
  String get ratingShort => 'Rating';

  @override
  String get export => 'Export';

  @override
  String get exportCsv => 'CSV';

  @override
  String get exportExcel => 'Excel';

  @override
  String get exportPdf => 'PDF';

  @override
  String get printReport => 'Print';

  @override
  String get printFailed =>
      'Printing failed. Check the printer, or export the PDF and open it yourself.';

  @override
  String savedTo(String path) {
    return 'Saved: $path';
  }

  @override
  String get showInFolder => 'Show';

  @override
  String get reportRevenue => 'Revenue';

  @override
  String get reportEmployees => 'Employees';

  @override
  String get reportServices => 'Services';

  @override
  String get reportProducts => 'Products';

  @override
  String get reportReservations => 'Appointments';

  @override
  String get reportOrders => 'Orders';

  @override
  String get reportCustom => 'Custom report';

  @override
  String get chooseSource => 'Data source';

  @override
  String get chooseFields => 'Fields';

  @override
  String get runReport => 'Show report';

  @override
  String get allStatusesOption => 'All statuses';

  @override
  String get noDataForPeriod => 'No data for the selected period.';

  @override
  String get pictures => 'Pictures';

  @override
  String get addPicture => 'Add a picture';

  @override
  String get changePicture => 'Change picture';

  @override
  String get removePicture => 'Remove picture';

  @override
  String get savePictureFirst =>
      'Pictures can be added once the item is saved.';

  @override
  String get clientSearchHint => 'Search by name, email or phone';

  @override
  String get clientRegistered => 'Registered';

  @override
  String get clientBlocked => 'Blocked';

  @override
  String get clientBlock => 'Block';

  @override
  String get clientUnblock => 'Unblock';

  @override
  String clientBlockQuestion(String name) {
    return 'Block client $name? They are signed out at once and cannot sign in until unblocked.';
  }

  @override
  String clientUnblockQuestion(String name) {
    return 'Unblock client $name?';
  }

  @override
  String get clientVisits => 'Visits';

  @override
  String get clientNoShows => 'No-shows';

  @override
  String get clientCancelled => 'Cancelled';

  @override
  String get clientUpcoming => 'Upcoming appointments';

  @override
  String get clientSpentServices => 'Spent on services';

  @override
  String get clientSpentProducts => 'Spent on products';

  @override
  String get clientLastVisit => 'Last visit';

  @override
  String get clientNever => 'Has not visited yet';

  @override
  String get clientRecentReservations => 'Recent appointments';

  @override
  String get clientRecentOrders => 'Recent orders';

  @override
  String get previousPage => 'Previous';

  @override
  String get nextPage => 'Next';

  @override
  String get auditLog => 'Activity log';

  @override
  String get auditSearchHint => 'Search by person, action or record';

  @override
  String get auditWhen => 'When';

  @override
  String get auditWho => 'Who';

  @override
  String get auditWhat => 'Action';

  @override
  String get auditTarget => 'Record';

  @override
  String get viewDay => 'Day';

  @override
  String get viewWeek => 'Week';

  @override
  String get previousWeek => 'Previous week';

  @override
  String get changePassword => 'Change password';

  @override
  String get currentPassword => 'Current password';

  @override
  String get repeatPassword => 'Repeat the new password';

  @override
  String get passwordsDoNotMatch => 'The passwords do not match.';

  @override
  String get nextWeek => 'Next week';

  @override
  String get cancelReasonLabel => 'Reason for cancelling';

  @override
  String get cancelReasonOptionalLabel => 'Reason (optional)';

  @override
  String get cancelReasonRequired =>
      'Enter a reason (at least 3 characters). The client receives it in a notification.';

  @override
  String get pictureNotSaved => 'The picture was not saved';

  @override
  String get chooseAccount => 'Who\'s signing in?';

  @override
  String get addAccount => 'Add account';

  @override
  String get removeFromList => 'Remove from list';

  @override
  String get backToAccounts => 'Back to accounts';

  @override
  String get mainPicture => 'Main';

  @override
  String get pictureHint =>
      'JPEG, PNG or WebP, up to 5 MB. The same picture cannot be added twice.';

  @override
  String get manageTitles => 'Edit title list';

  @override
  String get newTitle => 'New title';

  @override
  String titleUsedBy(int count) {
    return 'In use: $count';
  }

  @override
  String get titleInUseHint =>
      'Barbers still have this title; change theirs before deleting it.';

  @override
  String get gender => 'Gender';

  @override
  String get genderMale => 'Male';

  @override
  String get genderFemale => 'Female';

  @override
  String get done => 'Done';

  @override
  String get manageAccounts => 'Edit accounts';

  @override
  String get removeAccountHint =>
      'The account is only removed from the list on this computer.';

  @override
  String get zoom => 'Zoom';

  @override
  String get resetZoom => 'Reset';

  @override
  String get zoomHint =>
      'The picture centres on the face by itself. Zoom sets how close it gets.';

  @override
  String get galleryTitle => 'Work gallery';

  @override
  String get ourWork => 'Our work';

  @override
  String get viewGallery => 'View gallery';

  @override
  String get allStyles => 'All';

  @override
  String get galleryEmpty => 'No work added yet.';

  @override
  String get closedDay => 'Closed';

  @override
  String get openInMaps => 'Open in Google Maps';

  @override
  String get callSalon => 'Call';

  @override
  String get findUs => 'Find us';

  @override
  String get bookAppointment => 'Book an appointment';

  @override
  String get haircutStyle => 'Haircut style';

  @override
  String get doneBy => 'Barber';

  @override
  String get addWork => 'Add work';

  @override
  String get editWork => 'Edit work';

  @override
  String get captionOptional => 'Caption (optional)';

  @override
  String get showOnHome => 'Show on home screen';

  @override
  String get onHomeBadge => 'On home';

  @override
  String get noBarber => 'No barber';

  @override
  String get styles => 'Haircut styles';

  @override
  String get addStyle => 'Add style';

  @override
  String get styleName => 'Style name';

  @override
  String get chooseStyle => 'Choose a haircut style.';

  @override
  String get choosePicture => 'Choose a picture';

  @override
  String get deleteWorkQuestion => 'Delete this work from the gallery?';

  @override
  String get deleteStyleQuestion => 'Delete this style?';

  @override
  String get workAdded => 'Work added.';

  @override
  String get salonProfile => 'Salon';

  @override
  String get salonName => 'Salon name';

  @override
  String get address => 'Address';

  @override
  String get mapsLink => 'Google Maps link';

  @override
  String get logo => 'Logo';

  @override
  String get openFrom => 'From';

  @override
  String get openTo => 'To';

  @override
  String get openDay => 'Open';

  @override
  String get invalidUrl => 'Enter a valid web address (https://...).';

  @override
  String get hoursOrder => 'Opening time must be before closing time.';

  @override
  String get salonSaved => 'Salon details saved.';

  @override
  String get aboutSalon => 'About the salon';

  @override
  String get latitude => 'Latitude';

  @override
  String get longitude => 'Longitude';

  @override
  String get coordinatesBoth =>
      'Enter both latitude and longitude, or neither.';

  @override
  String get invalidCoordinate => 'Invalid value.';

  @override
  String get chooseServicesHint =>
      'Choose one or more services, then a barber and a time.';

  @override
  String get noBarberForServices =>
      'No barber does all of the chosen services. Go back and change your choice.';

  @override
  String get quantity => 'Quantity';

  @override
  String addedToCartCount(int count) {
    return 'Added to basket: $count';
  }

  @override
  String get allProducts => 'All products';

  @override
  String get salePrice => 'Sale price';

  @override
  String get salePriceHint => 'Leave empty when the product is not on sale';

  @override
  String get salePriceTooHigh => 'Must be lower than the regular price';

  @override
  String get shopSectionsTitle => 'Shop front page';

  @override
  String get shopSectionsHint =>
      'The rows customers see at the top of the shop. Drag to reorder.';

  @override
  String get addSection => 'Add section';

  @override
  String get editSection => 'Edit section';

  @override
  String get sectionTitle => 'Title';

  @override
  String get sectionKind => 'What it shows';

  @override
  String get kindNewArrivals => 'New arrivals (latest added products)';

  @override
  String get kindOnSale => 'On sale (products with a sale price)';

  @override
  String get kindCategory => 'Products of a category and/or brand';

  @override
  String get kindManual => 'Hand-picked products';

  @override
  String get sectionMaxItems => 'Most products';

  @override
  String get sectionVisible => 'Shown to customers';

  @override
  String get sectionPickProducts => 'Pick products';

  @override
  String get sectionSelected => 'Selected';

  @override
  String get noSections => 'No sections yet. Add the first one.';

  @override
  String get deleteSectionQuestion =>
      'Delete the section? The products stay in the shop.';

  @override
  String get searchProducts => 'Search products';

  @override
  String get mapsLinkHint =>
      'Paste a Google Maps share link. The map pin is read from the link; if that fails, enter latitude and longitude below.';

  @override
  String get paymentNotCompleted =>
      'Payment was not completed. No order was placed and your items are still in the basket.';

  @override
  String get groupWork => 'Work';

  @override
  String get groupShop => 'Shop';

  @override
  String get groupSalon => 'Salon';

  @override
  String get groupClients => 'Clients';

  @override
  String get groupAnalytics => 'Analytics';

  @override
  String get groupAccount => 'Account';

  @override
  String get any => 'Any';

  @override
  String get pickCategoryOrBrand => 'Pick a category or a brand';

  @override
  String get instagramLink => 'Instagram (link)';

  @override
  String get facebookLink => 'Facebook (link)';

  @override
  String get connectWithUs => 'Connect with us';

  @override
  String get findUsHere => 'Find us here';

  @override
  String get callUs => 'Call';

  @override
  String get instagram => 'Instagram';

  @override
  String get facebook => 'Facebook';

  @override
  String get eyebrowGallery => 'Gallery';

  @override
  String get eyebrowContact => 'Contact';

  @override
  String get eyebrowLocation => 'Location';

  @override
  String get eyebrowSchedule => 'Schedule';

  @override
  String get eyebrowAbout => 'About us';

  @override
  String get getToKnowUs => 'Get to know us';

  @override
  String welcomeTo(String name) {
    return 'Welcome to $name';
  }

  @override
  String get groupPersonalData => 'Personal data';

  @override
  String get groupAboutUs => 'About us';

  @override
  String get editProfile => 'Edit profile';

  @override
  String get manageAccount => 'Manage account';

  @override
  String get privacyPolicy => 'Privacy policy';

  @override
  String get termsOfUse => 'Terms of use';

  @override
  String get aboutApp => 'About the app';

  @override
  String get chooseDate => 'Choose a date';

  @override
  String get chooseTime => 'Choose a time';

  @override
  String get unavailableMarkedRed =>
      'Days with no free times are marked in red';

  @override
  String get eyebrowAssortment => 'Range';

  @override
  String get eyebrowBrands => 'Brands';

  @override
  String get brandsTitle => 'Brands';

  @override
  String get onSaleTitle => 'On sale';

  @override
  String get newInStoreTitle => 'New in store';

  @override
  String get newestProducts => 'Latest products';

  @override
  String get searchResults => 'Search results';

  @override
  String discountUpTo(int percent) {
    return 'up to -$percent%';
  }

  @override
  String get showAsCard => 'Show as a card on the entry page';

  @override
  String get cardPicture => 'Card picture';

  @override
  String get cardPictureAuto =>
      'Automatic picture (first product of the section)';

  @override
  String get uploadPicture => 'Upload a picture';

  @override
  String get pickProductPicture => 'Pick a product picture';

  @override
  String get saveSectionFirst => 'Save the section first, then add a picture.';

  @override
  String get pickPictureTitle => 'Choose a product picture';

  @override
  String get noPicturesFound => 'No products with pictures for this search.';

  @override
  String get brandLogoUpload => 'Set logo';

  @override
  String get brandLogoRemove => 'Remove logo';

  @override
  String get sectionTitleHint => 'A short title that fits on a card';

  @override
  String sectionTitleTooShort(int min) {
    return 'The title needs at least $min characters.';
  }

  @override
  String sectionTitleTooLong(int max) {
    return 'The title can have at most $max characters.';
  }

  @override
  String get sectionLayout => 'Section layout';

  @override
  String get layoutRow => 'Row';

  @override
  String get layoutGrid => 'Grid';

  @override
  String get layoutBanner => 'Big banner';

  @override
  String get layoutRowHint => 'Products in one row that scrolls sideways.';

  @override
  String get layoutGridHint => 'Products in two columns, one under the other.';

  @override
  String get layoutBannerHint =>
      'A wide picture with the title, and a row of products under it.';

  @override
  String get previewTitle => 'Preview in the app';

  @override
  String get previewCard => 'Card';

  @override
  String get previewSection => 'Section';

  @override
  String get previewSectionTitlePlaceholder => 'Section title';

  @override
  String get shopTab => 'Shop';

  @override
  String get sectionEyebrow => 'Small text above the title (yellow)';

  @override
  String get sectionEyebrowHint =>
      'Optional, e.g. COLLECTION. Shown in yellow letters.';

  @override
  String get sectionTitleWhite => 'Title (white text)';

  @override
  String get sectionEmptyWarning =>
      'This section currently has no products, so customers do not see it. Change what it selects.';

  @override
  String sectionHasProducts(int count) {
    return 'Currently shows $count products';
  }

  @override
  String get sectionEmptyShort =>
      'No products: customers do not see this section';

  @override
  String get seeAll => 'See all';

  @override
  String get kindCategoryTiles => 'Category block (tiles)';

  @override
  String get kindBrandTiles => 'Brand block (cards)';

  @override
  String get kindRecommended => 'Suggestions (\"You may like\")';

  @override
  String get styleTitle => 'Look';

  @override
  String get backgroundLabel => 'Section background';

  @override
  String get bgNone => 'No panel';

  @override
  String get bgSoft => 'Dark panel';

  @override
  String get bgAmber => 'Amber panel';

  @override
  String get bannerAlwaysPanel =>
      'A banner always stands on a panel, together with the products under it.';

  @override
  String get alignLabel => 'Title alignment';

  @override
  String get alignLeft => 'Left';

  @override
  String get alignCenter => 'Center';

  @override
  String get tileSizeLabel => 'Product size';

  @override
  String get tileCompact => 'Small';

  @override
  String get tileNormal => 'Medium';

  @override
  String get tileLarge => 'Large';

  @override
  String get showSeeAllLabel => 'Show a \"See all\" link under the section';

  @override
  String get cardSpanLabel => 'Card width';

  @override
  String get spanHalf => 'Half width';

  @override
  String get spanFull => 'Full width';

  @override
  String get tileCountCategories => 'Number of categories (even)';

  @override
  String get tileCountBrands => 'Number of brands';

  @override
  String get tilesManual => 'Pick and order by hand';

  @override
  String get tilesAutoHint =>
      'Automatic: only those with products are shown, the busiest first.';

  @override
  String get tilesPickHint =>
      'Tap to add, drag to reorder. Only those with products are shown.';

  @override
  String get tilesSelected => 'Selected';

  @override
  String get tilesAvailable => 'Available';

  @override
  String get categoriesEvenError =>
      'The number of chosen categories must be even so the last row is full.';

  @override
  String get blockNoDelete =>
      'This block is not deleted, only hidden with the switch.';

  @override
  String sectionShows(int count) {
    return 'Shows: $count';
  }

  @override
  String get previewSeeAll => 'See all';

  @override
  String get confirmOrderTitle => 'Confirm the order';

  @override
  String confirmOrderBody(String total, String payment) {
    return 'An order of $total. $payment. Pick-up at the salon.';
  }

  @override
  String get confirmBookingTitle => 'Confirm the booking';

  @override
  String confirmBookingBody(String barber, String when, String total) {
    return 'Book an appointment with $barber, $when, total $total?';
  }

  @override
  String get messagePicture => 'Picture (optional)';

  @override
  String get removePictureShort => 'Remove';

  @override
  String get salonLocation => 'Salon location on the map';

  @override
  String get salonLocationHint =>
      'Type an address and find it, or click the map to drop the pin.';

  @override
  String get searchAddress => 'Address to search';

  @override
  String get findOnMap => 'Find';

  @override
  String get addressNotFound =>
      'Address not found. Click the map to drop the pin.';

  @override
  String get noPinYet => 'No pin yet.';

  @override
  String get pinSet => 'The pin is set.';

  @override
  String get removePin => 'Remove pin';

  @override
  String confirmDeleteNamed(String name) {
    return 'Delete “$name”? This cannot be undone.';
  }

  @override
  String get removePictureQuestion => 'Remove this picture?';

  @override
  String fulfilOrderQuestion(String date, String client) {
    return 'Mark the order of $date ($client) as handed over? This cannot be undone.';
  }

  @override
  String announceQuestion(String audience) {
    return 'Send this message to: $audience? Recipients see it in the app right away.';
  }

  @override
  String announceEmailQuestion(String audience) {
    return 'Send this message to: $audience, also by e-mail? Sent e-mails cannot be recalled.';
  }

  @override
  String get completeQuestion => 'Mark the appointment as completed?';

  @override
  String get noShowQuestion => 'Mark that the client did not come?';

  @override
  String get sectionProductsCountError =>
      'Pick between 1 and 30 products for this section.';

  @override
  String get quantityLess => 'One less';

  @override
  String get quantityMore => 'One more';

  @override
  String get removeFromSelection => 'Remove from the selection';

  @override
  String get employeeSearchHint => 'Search by name or title';

  @override
  String get serviceSearchHint => 'Search by name or description';

  @override
  String get clearSearch => 'Clear search';

  @override
  String get renameTitle => 'Rename title';

  @override
  String get titleName => 'Title name';

  @override
  String get confirmBooking => 'Confirm';

  @override
  String get declineBooking => 'Decline';

  @override
  String get declineBookingTitle => 'Decline booking';

  @override
  String get declineBookingQuestion =>
      'Decline this booking? The time is freed and the client is told the reason.';

  @override
  String get statusDeclined => 'Declined';

  @override
  String get bookingConfirmedByStaff => 'The appointment is confirmed.';

  @override
  String get bookingDeclined => 'The booking was declined.';

  @override
  String get autoConfirmReservations => 'Confirm bookings automatically';

  @override
  String get autoConfirmReservationsHint =>
      'On: a booking is confirmed as soon as the client makes it. Off: it waits (the time is held) until a barber or the admin confirms it or declines it with a reason.';

  @override
  String get pendingBookingHint =>
      'The salon has not confirmed this appointment yet. The time is held for you.';

  @override
  String get changePhoto => 'Change picture';

  @override
  String get adjustPicture => 'Adjust picture';

  @override
  String get unlockAccount => 'Unlock';

  @override
  String lockedUntil(String time) {
    return 'Locked until $time';
  }

  @override
  String unlockAccountQuestion(String name) {
    return 'Unlock the account of $name? They can sign in again right away.';
  }

  @override
  String get accountUnlocked => 'The account is unlocked.';

  @override
  String get reviewWhat => 'What would you like to rate?';

  @override
  String get reviewTargetEmployee => 'Barber';

  @override
  String get reviewTargetService => 'Service';

  @override
  String get reviewTargetProduct => 'Product';

  @override
  String get announcementCategory => 'Category';

  @override
  String get categoryNews => 'News';

  @override
  String get categoryPromotion => 'Promotion';

  @override
  String get categoryNotice => 'Notice';

  @override
  String get expiresOn => 'Valid until';

  @override
  String get noExpiry => 'No end date';

  @override
  String get chooseExpiry => 'Choose end date';

  @override
  String get clearExpiry => 'Remove end date';

  @override
  String get announcementHidden => 'Hidden';

  @override
  String get announcementExpired => 'Expired';

  @override
  String get hideAnnouncement => 'Hide';

  @override
  String get showAnnouncement => 'Show again';

  @override
  String get hideAnnouncementQuestion =>
      'Hide the announcement? Recipients will no longer see it; you can show it again later.';

  @override
  String get editAnnouncement => 'Edit announcement';

  @override
  String get announcementSaved => 'Announcement saved.';

  @override
  String get searchAnnouncements => 'Search announcements';

  @override
  String get editNotResent =>
      'Edits are not sent again; recipients see the new text in the app.';

  @override
  String get emailOrUsername => 'Email or user name';
}

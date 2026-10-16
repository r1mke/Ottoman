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
  String get invalidEmail => 'Enter a valid email';

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
      'To pick services, first choose one of our barbers.';

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
  String get statusPending => 'Pending';

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
  String get delivery => 'Delivery';

  @override
  String get deliveryAddress => 'Delivery address';

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
  String orderNumber(int id) {
    return 'Order #$id';
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
    return '$count products';
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
  String get addPicture => 'Add picture';

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
  String get previousPage => 'Previous page';

  @override
  String get nextPage => 'Next page';

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
}

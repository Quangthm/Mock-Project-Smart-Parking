using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Guest;

internal static partial class PublicWorkflowQuestions
{
    // A named destination can describe why or where the driver parks, rather than what they book.
    internal static string Focus(string question)
    {
        question = BookingAfterParkingPurpose().Replace(question, "$1 cho gui xe $2");
        if (BookingAction().IsMatch(question))
        {
            question = OpeningParkingPurpose().Replace(question, "");
            return ParkingPurpose().Replace(question, "$1").Trim();
        }
        if (CarParkMap().IsMatch(question) && MapAction().IsMatch(question))
            return ParkingLandmark().Replace(question, "");
        return question;
    }

    internal static bool OutsideParking(string question) => UnrelatedSubject().IsMatch(question) ||
        AssistantAppearance().IsMatch(question) ||
        (InvoiceSubject().IsMatch(question) && Purchase().IsMatch(question) && !ParkingDocument().IsMatch(question)) ||
        HeadModifier().Matches(question).Any(match => !WorkflowModifier().IsMatch(match.Groups["modifier"].ValueSpan)) ||
        HasForeignQualifier(question, DocumentQualifier(), DocumentScope()) ||
        HasForeignQualifier(question, NoticeQualifier(), NoticeScope()) ||
        (MonthlyPass().IsMatch(question) && ForeignPassVenue().IsMatch(question)) ||
        ForeignDepartureVenue().IsMatch(question);

    private static bool HasForeignQualifier(string question, Regex qualifier, Regex scope) =>
        qualifier.Matches(question).Any(match => !scope.IsMatch(question.AsSpan(match.Index + match.Length)));

    // A transaction noun is neutral. When it names a service, that service must be in scope.
    private const string ParkingReference =
        @"(?:luot )?(?:gui xe|do xe|dau xe)|(?:tien|phi) (?:gui xe|do xe|dau xe|giu cho)|parking|dat cho|giu cho|booking|reservation|goi (?:thang|nam)|smart ?park";
    private const string Transaction =
        @"(?:giao dich thanh toan|giao dich|(?:da )?(?:thanh toan|tra tien)|transaction|payment|paid|paying)";
    private const string TransactionEnd =
        @"(?=$| (?:nay|do|o dau|o mo|where|thi|phai|kiem tra|xem|tren smartpark|on smartpark)\b)";
    private const string TransactionTarget =
        @"(?: (?:cua|cho|for|of))?(?: (?:my|the|a|this))? (?:" + ParkingReference + @")\b";
    private const string ScopedTransaction = Transaction + @"(?:" + TransactionEnd + "|" + TransactionTarget + ")";
    private const string TimedTransaction = @"(?:sau khi|after) " + ScopedTransaction;
    private const string NoticeTransactionFrame =
        @"(?:(?:sau khi|after|viec|that(?: i)?|xac nhan|confirmation of) )?";
    private const string WorkflowModifierTerm =
        @"(?:parking|smart ?park|booking|reservation|payment|transaction|fee|confirmation|confirmed|" +
        @"missing|recent|last|latest|previous|official|digital|pdf|online|email|sms)";

    internal static bool HasMultipleQuestions(string message, string question)
    {
        // Facts about earlier steps are context for the request after them.
        question = CompletedWorkflowContext().Replace(question, "");
        if (QuestionBoundary().IsMatch(message.TrimEnd())) return true;
        var sentences = SentenceBoundary().Split(message.TrimEnd());
        if (sentences.Length > 1 && sentences
            .Select(sentence => CompletedWorkflowContext().Replace(
                NormalizeWording(PublicFaqRetrieval.Normalize(sentence)), ""))
            .Count(sentence => RequestClause().IsMatch(sentence) || BusinessDataHandoff.Match(sentence) is not null) > 1) return true;
        if (MultipleQuestions().IsMatch(question)) return true;
        var clauses = Conjunction().Split(question);
        if (clauses.Length < 2) return false;
        var parkingContext = clauses.Any(clause => BusinessDataHandoff.Match(clause) is not null);
        string? firstTopic = null;
        foreach (var clause in clauses)
        {
            var topic = BusinessDataHandoff.Match(clause, parkingContext);
            if (topic is null && AsksLiveParking(clause)) topic = "dynamic";
            if (topic is null && AsksAboutMissingNotice(clause)) topic = "notice";
            if (topic is null && ProductOverview().IsMatch(clause)) topic = "smartpark-overview";
            if (topic is null)
            {
                if (WorkflowSubject().Match(clause) is not { Success: true } match) continue;
                topic = WorkflowSubject().GetGroupNames().Skip(1).First(name => match.Groups[name].Success);
            }
            if (firstTopic is not null && firstTopic != topic) return true;
            firstTopic = topic;
        }
        return false;
    }

    internal static string? MatchPublicGuidance(string question)
    {
        if (DriverSignUp().IsMatch(question)) return "driver-signup-navigation";
        if (EnglishBookingLocation().IsMatch(question)) return "booking-navigation";
        if (CancellationSteps().IsMatch(question)) return "booking-cancellation-steps";
        if (BookingConfirmation().IsMatch(question)) return "booking-start";
        if (ReservationNavigation().IsMatch(question)) return "reservation-navigation";
        if (MapNavigation().IsMatch(question)) return "discovery-navigation";
        if (EntryConfirmation().IsMatch(question)) return "parking-entry-confirmation";
        if (PublicOverstay().IsMatch(question)) return GuestIntents.ParkingOverstay;
        if (PublicLateArrival().IsMatch(question)) return "parking-arrive-late";
        if (LostTicketVerification().IsMatch(question)) return "lost-parking-ticket";
        if (DuplicateParkingCharge().IsMatch(question)) return "parking-charge-dispute";
        if (UncertainRefund().IsMatch(question)) return "refund-status-guidance";
        if (PublicWalkIn().IsMatch(question)) return "parking-walk-in";
        if (PublicAccountRecovery().IsMatch(question)) return "account-recovery";
        if (PublicMissingNotice().IsMatch(question)) return "parking-notice-missing";
        if (SiteTicketAvailability().IsMatch(question)) return "parking-site-support";
        if (PublicOwnerOnboarding().IsMatch(question)) return "owner-onboarding";
        if (PublicSiteApproval().IsMatch(question)) return "site-approval";
        if (OperatorScope().IsMatch(question)) return "operator-scope";
        return null;
    }

    internal static string? MatchAction(string question)
    {
        if (CancelReservation().IsMatch(question)) return "booking-cancel-action";
        if (CreateReservation().IsMatch(question)) return "booking-create-action";
        return null;
    }

    internal static bool AsksLiveParking(string question) =>
        (LivePlace().IsMatch(question) && LiveAvailability().IsMatch(question)) || LivePrice().IsMatch(question);

    private static bool AsksAboutMissingNotice(string question) =>
        MissingNotice().IsMatch(question) && (Notice().IsMatch(question) || BookingReminder().IsMatch(question));

    internal static bool AsksForAnotherAccountsRecords(string question) =>
        AccountRecordRequest().IsMatch(question) && AnotherAccount().IsMatch(question);

    internal static string NormalizeWording(string question)
    {
        question = RegionalWords().Replace(question, match => match.Value switch
        {
            "ni" => "nay",
            "chi" => "gi",
            "tui" => "toi",
            "hong" or "hongg" or "khongg" => "khong",
            "lam rang" => "lam sao",
            "lay rang" => "lay o dau",
            "coi" => "xem",
            "dau xe" => "do xe",
            "giu slot" => "giu cho",
            "slot parking" => "cho do xe",
            "bay chu" => "bay gio",
            "lich giu cho" => "lich dat",
            "bien nhan" => "bien lai",
            "tre hon gio" or "muon hon gio" => "sau gio",
            _ => match.Value
        });
        return PoliteEnding().Replace(question.Trim(), "");
    }

    internal static string? Match(string message, string question)
    {
        if (question.Length > 320 ||
            message.AsSpan().TrimEnd().TrimEnd("?!.".AsSpan()).IndexOfAny("?!.;".AsSpan()) >= 0)
            return null;

        if (AssistantIdentity().IsMatch(question)) return "assistant-identity";
        if (ProductOverview().IsMatch(question)) return "smartpark-overview";
        if (LostParkingTicket().IsMatch(question)) return "lost-parking-ticket";
        if (IncorrectParkingCharge().IsMatch(question)) return "parking-charge-dispute";
        if (InvoiceSubject().IsMatch(question) && DocumentAccess().IsMatch(question)) return "invoice-navigation";
        if (ParkingSupport().IsMatch(question)) return "parking-site-support";
        if ((ExitSubject().IsMatch(question) || ExitBarrier().IsMatch(question)) &&
            (HowTo().IsMatch(question) || Paid().IsMatch(question)))
            return Blocked().IsMatch(question) ? "parking-exit-blocked" : "parking-departure";
        if (Refund().IsMatch(question) && Cancellation().IsMatch(question)) return "booking-refund";
        if (LateArrival().IsMatch(question) && Arrival().IsMatch(question)) return "parking-arrive-late";
        if (PriorBooking().IsMatch(question) && DirectEntry().IsMatch(question)) return "parking-walk-in";
        if (CarParkMap().IsMatch(question) && MapAction().IsMatch(question)) return "discovery-navigation";
        if (MonthlyPass().IsMatch(question) && !PassCancellation().IsMatch(question)) return "monthly-yearly-guide";
        if (VehicleType().IsMatch(question) && VehicleQuestion().IsMatch(question)) return "vehicle-guide";
        if (AsksAboutMissingNotice(question)) return "parking-notice-missing";
        if (BookingLocation().IsMatch(question) && !Cancellation().IsMatch(question) &&
            (BookingAction().IsMatch(question) || BareBookingQuestion().IsMatch(question)))
            return "booking-navigation";
        if (BareBookingQuestion().IsMatch(question) || ParkingPurposeQuery().IsMatch(question))
            return "booking-start";
        if (BookingAction().IsMatch(question) && (HowTo().IsMatch(question) || WhereToTap().IsMatch(question) || ParkingEligibility().IsMatch(question)) &&
            !Cancellation().IsMatch(question))
            return "booking-start";
        return null;
    }

    [GeneratedRegex(@"\b(?:xem phim|rap phim|ve phim|tien dien|tien nuoc|hoa don (?:dien|nuoc)|dien luc|tien (?:internet|mang)|nau an|kho ca|bai bien|dat phong|khach san|ve may bay|nha hang|an toi|hoc dan|cat toc|du bao thoi tiet|bao hiem|rua xe|tuyen dung|xe buyt|hotel rooms?|cinema|movie tickets?|(?:electric(?:ity)?|water|internet) bills?|flights?|restaurants?|weather forecast|insurance|car wash|job notifications?|bus passes?)\b")]
    private static partial Regex UnrelatedSubject();

    [GeneratedRegex(@"\b(?:ban|bot|tro ly) (?:co )?(?:dep trai|xinh gai|dep gai)\b|\bare you (?:handsome|pretty|beautiful)\b")]
    private static partial Regex AssistantAppearance();

    [GeneratedRegex(@"\s+(?:khi|de) (?:di )?(?:xem phim|an toi|an tai nha hang|den khach san|hoc dan)" +
        @"(?: (?!(?:thi|co|duoc|the|lam|bam|va|hay|roi|and|or)\b)[\p{L}\p{N}]+){0,3}" +
        @"( (?:thi )?(?:co duoc khong|duoc khong|the nao|lam the nao|lam sao|bam dau|bam o dau))?$")]
    private static partial Regex ParkingPurpose();
    [GeneratedRegex(@"\Acho gui xe (?:khi|de) (?:di )?(?:xem phim|an toi) (?:thi )?(dat|book) (kieu (?:gi|chi)|the nao|lam sao|o dau|bam (?:o )?dau)\z")]
    private static partial Regex BookingAfterParkingPurpose();
    [GeneratedRegex(@"\A(?:di )?(?:an toi|xem phim|den nha hang) thi ")]
    private static partial Regex OpeningParkingPurpose();
    [GeneratedRegex(@"\b(?:gan|near) (?:khach san|nha hang|(?:a |the )?(?:hotel|restaurant))\s+")]
    private static partial Regex ParkingLandmark();
    [GeneratedRegex(@"\b(?:co duoc khong|duoc khong)$")]
    private static partial Regex ParkingEligibility();

    [GeneratedRegex(@"\b(?:lam rang|lay rang|dau xe|tre hon gio|muon hon gio|giu slot|slot parking|bay chu|lich giu cho|bien nhan|ni|chi(?= (?:rua|he)$|$)|tui|hongg?|khongg|coi)\b")]
    private static partial Regex RegionalWords();

    [GeneratedRegex(@"(?: (?:a|ta|rua|he|hen|vay|nhi|nhe|nha|ha|voi))+$")]
    private static partial Regex PoliteEnding();

    [GeneratedRegex(@"^(?:ua )?(?:(?:ban|mi|may|tro ly nay|bot nay) la (?:ai|gi|tro ly gi|bot gi)(?: assistant cua app)?|(?:who|what) (?:are you|is this assistant)|what kind of (?:bot|assistant) are you|(?:toi|minh) dang (?:chat|noi chuyen) voi (?:bot|tro ly) nao(?: trong smartpark)?|(?:ban|(?:con )?bot nay) (?:co kha nang|phu duoc viec|giup duoc) gi(?: trong app)?)(?: the| day)?$")]
    private static partial Regex AssistantIdentity();

    [GeneratedRegex(@"^(?:(?:cho (?:toi|minh) biet )?(?:app )?smart ?park la (?:platform |he thong |ung dung )?gi|(?:cai )?(?:he thong|app|ung dung|nen tang|phan mem|dich vu)(?: (?:gui xe|do xe|giu cho do xe))? nay (?:la (?:cai )?gi|dung de lam gi|giup (?:minh )?(?:duoc )?gi(?: cho nguoi gui xe)?)|cai nay la (?:app|ung dung) gi cho (?:tai xe|nguoi gui xe)|(?:what is|what does) (?:this|the) (?:system|app|platform|service)(?: do)?|smart ?park (?:lam duoc|giup duoc) gi cho (?:nguoi gui xe|tai xe)|is smart ?park (?:a )?parking service or (?:just )?(?:a )?chat window)(?: the)?$")]
    private static partial Regex ProductOverview();

    [GeneratedRegex(@"[?!;]\s*(?=\p{L})")]
    private static partial Regex QuestionBoundary();
    [GeneratedRegex(@"(?<!\d)\.\s+(?=\p{L})|,\s*(?=\p{L})")]
    private static partial Regex SentenceBoundary();
    [GeneratedRegex(@"\A(?:(?:please )?(?:how|where|what|can i|show|find|tell)|cho xem|cho (?:toi|minh|tui) (?:xem|coi)|cach|lam sao|tim bai|(?:hien |bay gio )?bai)\b|\b(?:la gi|the nao|sao|o dau|kieu gi|khong)\z")]
    private static partial Regex RequestClause();
    [GeneratedRegex(@"\b(?:va|hay|and|or) (?:cach|lam sao|o dau|how|where|whether|what)\b")]
    private static partial Regex MultipleQuestions();
    [GeneratedRegex(@"\s+(?:va|hay|voi lai|voi(?= (?:chi|cach|cho|xem|coi|huong dan|lam sao|thanh toan|tra phi|gia gui xe)\b)|roi(?= (?:chi cach|cho xem|xem|huy|cap lai)\b)|and|or)\s+")]
    private static partial Regex Conjunction();
    private const string BookingOrPayment = @"(?:thanh toan|tra tien|dat cho|" + ExplicitParkingBooking + ")";

    [GeneratedRegex(
        @"^(?:(?:toi|minh|tui) )?(?:da " + BookingOrPayment + @"(?: xong)?|" + BookingOrPayment + @" xong) (?:va|hay) " +
        @"|^(?:(?:toi|minh) )?(?:da )?(?:thanh toan|tra|dong) (?:tien|phi) (?:gui|do) xe (?:xong |roi )(?:(?:va|thi) )?" +
        @"|^sau khi (?:thanh toan|tra tien|dat cho)(?: xong)?(?: va duoc (?:duyet|xac nhan))? thi " +
        @"|^i (?:have )?(?:already )?(?:paid|booked) and ")]
    private static partial Regex CompletedWorkflowContext();
    [GeneratedRegex(@"\b(?:(?<invoice>hoa don|chung tu|bien lai|invoice|receipt)|(?<booking>dat cho|dat xe|book|reserve)(?! truoc\b)|(?<entry>vao bai|gui xe truc tiep|walk in|without booking)|(?<payment>tra phi|tra tien|dong (?:them )?tien|dong phi|thanh toan|gia gui xe|pay|payment)|(?<refund>hoan tien|lay lai tien|refund|huy cho|huy (?:lich dat|dat cho)|cancel (?:a |my )?(?:booking|reservation))|(?<arrival>toi tre|den tre|toi muon|den muon|arrive late)|(?<exit>ra cong|ra (?:khoi )?bai|exit gate)|(?<reservations>lich dat|my (?:bookings|reservations)|what i booked)|(?<vehicles>my (?:(?:saved|registered) )?vehicles|xe cua (?:toi|minh|tui))|(?<search>tim bai|find parking)|(?<notice>thong bao|notification)|(?<ticket>cap lai ve gui xe)|(?<assistant>ban la (?:ai|gi)|who are you))\b")]
    private static partial Regex WorkflowSubject();
    private const string Reservation = @"(?:lich dat(?: cho)?|dat cho|cho da dat|booking|reservation)";
    [GeneratedRegex(@"\A(?:(?:cach|huong dan|chi (?:toi|minh|tui) cach|lam sao de) (?:huy|bo) " + Reservation +
        @"(?: (?:cua (?:toi|minh|tui)|tren smartpark))?|(?:muon )?(?:huy|bo) " + Reservation +
        @"(?: (?:gui|do) xe)?(?: cua (?:toi|minh|tui))? (?:thi )?(?:lam sao|lam rang|the nao|nhu (?:the )?nao|ra sao|sao|cach nao|o dau)|(?:how|where) (?:to|do i|can i) cancel (?:my |a |the )?(?:booking|reservation))(?: (?:a|vay|rua|please|nhi))?\z")]
    private static partial Regex CancellationSteps();
    [GeneratedRegex(@"\A(?:tren app )?(?:giu cho|dat cho) roi co (?:can|phai) (?:bam )?xac nhan (?:gi )?(?:nua )?khong\z")]
    private static partial Regex BookingConfirmation();
    [GeneratedRegex(@"\A(?:(?:(?:toi|minh|tui) (?:co the )?)?(?:xem|coi) lich dat(?: cua (?:toi|minh|tui))? (?:o dau|o mo|the nao)|(?:cach|lam sao de|mo (?:muc|cho) nao de) (?:xem|coi) lich dat(?: cua (?:toi|minh|tui))?|(?:where|how) (?:do|can) i (?:view|see|find) my (?:bookings|reservations))\z")]
    private static partial Regex ReservationNavigation();
    [GeneratedRegex(@"\A(?:(?:cach|huong dan|lam sao de) tim bai(?: do| xe)?(?: tren ban do)?|tim bai(?: do| xe)?(?: tren ban do)? (?:the nao|lam sao|lam rang|o dau|ntn)|(?:how (?:to|do i|can i) )?(?:find|search for) parking (?:on (?:the )?map|step by step)|how (?:to|do i|can i) (?:find parking|use the map to find (?:parking|(?:a )?nearby (?:garage|parking lot)))|can i (?:pick|choose|enter) (?:my own|a) starting point when i search (?:the map|for parking))\z")]
    private static partial Regex MapNavigation();

    [GeneratedRegex(@"\A(?:(?:hay |vui long |toi muon )?(?:huy|bo|cancel)(?: (?:giup|cho) (?:toi|minh|tui))? (?:cai )?" + Reservation +
        @"(?: cua (?:toi|minh|tui))?(?: (?:ngay(?: bay gio)?|giup (?:toi|minh|tui)|di|voi|nhe))?|(?:please )?cancel my (?:booking|reservation)(?: now| please)?)[ ]*\z")]
    private static partial Regex CancelReservation();
    [GeneratedRegex(@"\A(?:(?:hay |vui long )?dat (?:cho )?giup (?:toi|minh|tui)(?: (?:mot )?cho)?|(?:please )?book (?:a parking spot|parking) for me)(?: (?:gui xe|do xe|dau xe|nhe|voi|please))*\z")]
    private static partial Regex CreateReservation();
    [GeneratedRegex(@"\b(?:bai|parking|cho (?:do|dau)|(?:cho|o) (?:nao )?(?:do|dau) (?:xe|duoc)|o trong de gui)\b")]
    private static partial Regex LivePlace();
    [GeneratedRegex(@"\b(?:(?:con|het) (?:cho|trong|bai|slot)|con nhan xe|cho trong|o trong|available spaces|availability|free bays)\b|\b(?:luc nay|bay gio|right now|near me|quanh day)\b.*\b(?:dau|do|park|space|nhan)\b|\b(?:dau|do|park|space|(?:con|dang|bat) nhan)\b.*\b(?:luc nay|bay gio|gio nay|tai thoi diem nay|right now|near me|quanh day)\b")]
    private static partial Regex LiveAvailability();
    [GeneratedRegex(@"\b(?:(?:gia|phi|tien) (?:gui|do|dau) xe|parking (?:price|fee|rate))\b.{0,50}\b(?:hien tai|luc nay|bay gio|right now|at the moment)\b|" +
        @"\A(?:(?:cho(?: (?:toi|minh))? |(?:toi|minh) )?(?:xem|hoi|kiem tra|tra cuu) )?" +
        @"(?:gia(?: tien)?|phi) (?:cua )?bai (?:nay|do)(?: (?:ngay|hien tai|luc nay|bay gio))?\z")]
    private static partial Regex LivePrice();
    [GeneratedRegex(@"\A(?:(?:cho |giup |xin |co the )?(?:toi|minh) )?(?:cho )?(?:xem|liet ke|tra cuu|kiem tra|show|list|view|look up)\b.{0,55}\b(?:xe|bien so|lich dat|bookings?|reservations?|vehicles?|plates?|hoa don|chung tu|receipt|invoice)\b")]
    private static partial Regex AccountRecordRequest();
    [GeneratedRegex(@"\b(?:cua (?:nguoi khac|tai khoan khac|vo toi|chong toi|ban toi|dong nghiep(?: cua)? toi)|someone else|another account|my (?:wife|husband|friend|colleague) s)\b")]
    private static partial Regex AnotherAccount();
    [GeneratedRegex(@"\Ahow (?:are|do) (?:parking )?(?:fees|charges) (?:determined|calculated) if i stay past (?:my|the) (?:booking|reservation)\z")]
    private static partial Regex PublicOverstay();
    [GeneratedRegex(@"\Amo cong roi co (?:duoc )?tinh la da vao bai chua\z")]
    private static partial Regex EntryConfirmation();
    [GeneratedRegex(@"\Ai lost (?:my|the) parking ticket is (?:the )?(?:license )?plate alone enough\z")]
    private static partial Regex LostTicketVerification();
    [GeneratedRegex(@"\A(?:the )?(?:parking lot|car park) charged me twice (?:where|how) can i report (?:it|this)\z")]
    private static partial Regex DuplicateParkingCharge();
    [GeneratedRegex(@"^what (?:can|should) i do if i arrive late for (?:my|the|a) (?:booking|reservation)$|\A(?:neu )?(?:den|toi) gio hen ma (?:chua|khong) (?:den|toi) kip thi (?:lam )?sao\z|\A(?:neu )?(?:den|toi) tre(?: qua gio bat dau lich dat)? (?:thi )?(?:kiem|xem) han(?: den)? o dau\z|\Aket (?:duong|xe) chua (?:toi|den) kip gio hen bai co ghi vang mat(?: luon)? khong\z")]
    private static partial Regex PublicLateArrival();
    [GeneratedRegex(@"\A(?:(?:(?:toi|minh) thay )?(?:app|smartpark|he thong) bao (?:da )?(?:chap thuan )?hoan(?: tien)? roi (?:vay )?tien (?:da ve chua|co ve (?:lien|ngay) khong)|how do i (?:tell|check) whether my refund (?:actually )?(?:went through|was paid))\z")]
    private static partial Regex UncertainRefund();
    [GeneratedRegex(@"^(?:neu )?(?:(?:toi|minh|tui) )?quen (?:mat )?mat khau(?: roi)?(?:(?: thi)? (?:khoi phuc bang cach nao|lam sao|lam the nao|phai lam sao|xu ly sao))?$|^(?:how do i|how can i) (?:recover|reset) (?:a|my|the) password$")]
    private static partial Regex PublicAccountRecovery();
    [GeneratedRegex(@"^can i (?:just )?(?:come|go|show up) (?:to|at) (?:a|the) (?:parking lot|car park) and (?:leave|park) my (?:car|vehicle) without (?:booking|a reservation)$")]
    private static partial Regex PublicWalkIn();
    [GeneratedRegex(@"\Ai (?:did not|didn t|never) (?:receive|get) (?:a|the) (?:parking )?(?:booking|payment) (?:notification|confirmation message) (?:what should i (?:do|check)|how can i check)\z")]
    private static partial Regex PublicMissingNotice();
    [GeneratedRegex(@"\A(?:tro ly|chatbot|bot) co (?:the )?(?:lam giup|tao|gui) (?:cai |mot )?(?:ticket|yeu cau ho tro) (?:bao )?su co (?:tai |trong )?bai (?:duoc )?khong\z")]
    private static partial Regex SiteTicketAvailability();
    [GeneratedRegex(@"^(?:toi muon |cach |lam sao de )?dang ky (?:lam )?(?:chu bai|doi tac bai do)(?: (?:the nao|nhu the nao|tren smartpark))?$|^how (?:do|can) i register as a (?:parking )?lot owner$")]
    private static partial Regex PublicOwnerOnboarding();
    [GeneratedRegex(@"^(?:quy trinh |cac buoc )?(?:admin )?(?:xet duyet|phe duyet) (?:ho so )?(?:bai do|bai xe)(?: (?:the nao|nhu the nao|tren smartpark))?$")]
    private static partial Regex PublicSiteApproval();
    [GeneratedRegex(@"^(?:(?:operator|nhan vien bai xe) (?:lam(?: duoc)? gi|co nhiem vu gi|duoc lam gi)|what (?:can a parking operator do|does a parking operator do))$")]
    private static partial Regex OperatorScope();
    [GeneratedRegex(@"\A(?:(?:cach|lam sao de|huong dan) )?dang ky (?:tai khoan (?:nguoi lai|driver|tai xe)|lam (?:tai xe|driver))(?: tren (?:ung dung |app )?smartpark)?(?: (?:can )?(?:bat dau tu dau|the nao|lam sao|o dau))?\z")]
    private static partial Regex DriverSignUp();
    [GeneratedRegex(@"\b(?:hoa don|chung tu|bien lai|invoice|receipt)\b")]
    private static partial Regex InvoiceSubject();
    [GeneratedRegex(@"\b(?:mua|buy|bought|purchas(?:e|ed|ing))\b")]
    private static partial Regex Purchase();
    [GeneratedRegex(@"\b(?:gui xe|do xe|dau xe|goi (?:thang|nam)|parking)\b")]
    private static partial Regex ParkingDocument();
    // English also uses compounds where the object qualifies the head from the left.
    [GeneratedRegex(@"\b(?:see|view|find|download|access|show me|get|receive|where (?:is|are)) " +
        @"(?>(?:(?:my|the|a|an|this|your) )?)(?<modifier>\p{L}+(?: \p{L}+){0,7}?) (?:invoice|receipt|notifications?)\b")]
    private static partial Regex HeadModifier();
    [GeneratedRegex(@"\A" + WorkflowModifierTerm + @"(?: " + WorkflowModifierTerm + @")*\z")]
    private static partial Regex WorkflowModifier();
    [GeneratedRegex(@"\b(?:hoa don|chung tu|bien lai|invoice|receipt) (?>(?:for |of |cho |cua )?)(?>(?:(?:my|the|a|mot|this) )?)(?=\p{L})")]
    private static partial Regex DocumentQualifier();
    [GeneratedRegex(@"\A(?:(?:viec (?:da )?)?(?:" + ParkingReference + @")\b|(?:confirmed|verified) parking payment\b|" +
        @"(?:bai (?:gui|do|dau) xe|o dau|o mo|where|pdf|nay|do|toi|minh|voi|nhi|nhe|a|rua|xem|coi|ghi cao hon (?:tien|phi) (?:gui|do|dau) xe)\b|" +
        ScopedTransaction + "|" + TimedTransaction + "|" + Transaction + " " + TimedTransaction + ")")]
    private static partial Regex DocumentScope();
    [GeneratedRegex(@"\b(?:xem|kiem|lay|tai|mo|o dau|o mo|cho mo|view|see|find|download|where)\b")]
    private static partial Regex DocumentAccess();
    [GeneratedRegex(@"\b(?:ra cong|cong ra|ra (?:khoi )?bai|lay xe (?:ra|ve)|chay xe ra|exit gate|leave the (?:car park|parking lot))\b")]
    private static partial Regex ExitSubject();
    [GeneratedRegex(@"\b(?:barie|barrier|thanh chan)\b.*\b(?:chua nhac|chua nhac len|chua nhac len duoc|chua nhuc nhich|khong nhac|chua mo|khong mo|chua len)\b")]
    private static partial Regex ExitBarrier();
    [GeneratedRegex(@"\b(?:chay xe|lai xe|danh xe) ra khoi (?!bai\b|khu (?:gui|do|dau) xe\b|cong\b)\p{L}")]
    private static partial Regex ForeignDepartureVenue();
    [GeneratedRegex(@"\b(?:da thanh toan|da tra tien|tra tien xong|thanh toan xong|after paying|pay xong|after (?:i )?pay)\b")]
    private static partial Regex Paid();
    [GeneratedRegex(@"\b(?:khong mo|khong mo duoc|chua mo|chua nhac|khong nhac|chua len|doesn t open|did not open|won t open)\b")]
    private static partial Regex Blocked();
    [GeneratedRegex(@"\b(?:lam sao|lam gi|cach|huong dan|cac buoc|kieu (?:gi|chi)|nhu nao|nhu the nao|the nao|ntn|sao|how|steps|what should)\b")]
    private static partial Regex HowTo();
    [GeneratedRegex(@"\b(?:huy (?:cho|dat cho|booking)|bo cho da dat|cancel (?:booking|a reservation))\b")]
    private static partial Regex Cancellation();
    [GeneratedRegex(@"\b(?:hoan tien|tra lai tien|lay lai tien|refund)\b")]
    private static partial Regex Refund();
    [GeneratedRegex(@"\b(?:toi|den) (?:bai|cong)\b|\barriv(?:e|ing)\b")]
    private static partial Regex Arrival();
    [GeneratedRegex(@"\b(?:sau gio|qua gio hen|tre gio|tre|late)\b")]
    private static partial Regex LateArrival();
    [GeneratedRegex(@"\b(?:khong|ko|chua) (?:dat truoc|book(?: truoc)?)\b|\bwithout (?:booking|a reservation)\b")]
    private static partial Regex PriorBooking();
    [GeneratedRegex(@"\b(?:gui xe|vao bai|vao cong|vo bai|park|walk in)\b")]
    private static partial Regex DirectEntry();
    [GeneratedRegex(@"\b(?:bai (?:do |dau )?xe|bai do|(?:cac )?bai|(?:cho )?gui xe|parking)\b")]
    private static partial Regex CarParkMap();
    [GeneratedRegex(@"\b(?:ban do|map)\b.*\b(?:xem|tim|mo|where|find|see)\b|\b(?:xem|tim|mo|where|find|see)\b.*\b(?:ban do|map)\b")]
    private static partial Regex MapAction();
    [GeneratedRegex(@"\b(?:goi (?:thang|nam)|ve (?:gui xe )?(?:theo )?(?:thang|nam)|gui xe theo thang|monthly (?:parking )?pass|yearly (?:parking )?pass)\b")]
    private static partial Regex MonthlyPass();
    [GeneratedRegex(@"\b(?:o|tren|tai) (?:ung dung|app) (?!smart ?park\b|gui xe\b|do xe\b|dau xe\b|nay\b)\p{L}")]
    private static partial Regex ForeignPassVenue();
    [GeneratedRegex(@"\b(?:huy|thoi dung|tra lai tien|hoan tien|dong cua|cancel|refund|clos(?:e|es|ure))\b")]
    private static partial Regex PassCancellation();
    [GeneratedRegex(@"\b(?:(?:loai|dong) xe|vehicle types)\b")]
    private static partial Regex VehicleType();
    [GeneratedRegex(@"\b(?:nhan|ho tro|phuc vu|chap nhan|nao|may|mo|support|accept|which)\b")]
    private static partial Regex VehicleQuestion();
    [GeneratedRegex(@"\b(?:(?:dat|book)(?: truoc)? (?:mot )?cho (?:do xe|dau xe|gui xe)|(?:dat|book) cho (?:dau|do|xe may|o to)|book (?:mot |a )?parking spot|reserve (?:a )?parking (?:spot|space)|reserve mot o cho (?:o to|xe may)|de danh (?:truoc )?cho (?:do|dau) xe|giu (?:mot )?(?:cho gui xe|suat (?:do|dau)(?: xe)?))\b")]
    private static partial Regex BookingAction();
    [GeneratedRegex(@"\A(?:(?:cho (?:toi|minh|tui) hoi |cho hoi )?(?:(?:toi|minh|tui) )?(?:muon )?(?:dat cho(?: (?:xe may|o to))?|dat truoc|book(?: cho)?|giu (?:mot )?cho|dat (?:mot |duoc cai )?o (?:de xe|tren app))(?: (?:thi|phai|bat dau))? (?:lam sao|lam the nao|lam cach nao|lam gi|the nao|o dau|o cho nao trong app|sao|kieu (?:gi|chi)|ra sao|nhu (?:the )?nao|bam (?:o )?dau)|(?:how (?:can|do) i|how to) reserve somewhere to park(?: in advance)?)\z")]
    private static partial Regex BareBookingQuestion();
    [GeneratedRegex(@"\Awhere (?:do|can|should) i (?:start to )?(?:book|reserve) " +
        @"(?:parking|(?:a )?parking (?:spot|space)|(?:a )?(?:parking )?(?:spot|space) for (?:(?:my|a|the) )?(?:car|motorcycle|motorbike))" +
        @"(?: (?:in|on|with) smartpark)?\z")]
    private static partial Regex EnglishBookingLocation();
    [GeneratedRegex(@"\A(?:dat|giu) (?:cho|mot (?:cho|suat)) (?:de |gui )?(?:gui xe|do xe|dau xe)\z")]
    private static partial Regex ParkingPurposeQuery();
    [GeneratedRegex(@"\b(?:bam|nhan|chon|thao tac) (?:o )?dau\b")]
    private static partial Regex WhereToTap();
    [GeneratedRegex(@"\b(?:(?:bam|vao|vo) (?:o )?dau|o dau\z|o cho nao trong app|vao muc nao|bat dau (?:tu dau|o man hinh nao)|vao dau truoc|where (?:do|can|should) i (?:start|book|reserve))\b")]
    private static partial Regex BookingLocation();
    [GeneratedRegex(@"\b(?:mat|roi|rot|that lac) (?:cai )?ve (?:giu xe|gui (?:xe|o to|xe may)|do xe|dau xe)\b|\bve (?:giu xe|gui (?:xe|o to|xe may)|do xe).{0,24}\b(?:roi|rot|that lac)\b|\b(?:mat|roi|rot|that lac) ve\b.*\b(?:lay xe|ra bai|ra cong)\b|\b(?:lost|misplaced) (?:my|the|a) parking ticket\b")]
    private static partial Regex LostParkingTicket();
    [GeneratedRegex(@"\A(?:(?:cho (?:toi|minh|tui) hoi |cho hoi )?(?:cach )?)?(?:bao|khieu nai) (?:viec )?thu sai(?: phi)?(?: (?:o dau|the nao|lam sao))?\z|\bbien lai ghi cao hon (?:tien|phi) (?:gui|do|dau) xe\b|\b(?:bai (?:gui |do |dau )?xe|(?:tien|phi) (?:gui|do|dau) xe)\b.*\b(?:thu sai|tinh sai)\b|\b(?:car park|parking lot)\b.*\b(?:wrong amount|charged (?:me )?twice|overcharged)\b")]
    private static partial Regex IncorrectParkingCharge();
    [GeneratedRegex(@"\A(?:(?:toi|minh) )?(?:gap|co) su co (?:trong|tai|o) bai(?: gui xe| do xe)? (?:hoi|lien he|nho) ai(?: xu ly)?\z|\Awhere (?:do|can) i get help with (?:an |a )?(?:issue|problem) at (?:a |the )?smartpark (?:lot|car park|parking lot)\z")]
    private static partial Regex ParkingSupport();
    private const string ExplicitParkingBooking = @"(?:(?:dat|book) cho (?:gui|do) xe|(?:dat cho|booking) (?:tren|in|on) smartpark)";
    private const string BookingEvent = @"(?:dat cho|booking|" + ExplicitParkingBooking + ")";
    private const string BookingNotice = @"(?:tin nhac|mail xac nhan|email xac nhan|email nhac)";

    [GeneratedRegex(@"\b" + BookingEvent + @"\b.*\b" + BookingNotice + @"\b|\b" +
        BookingNotice + @" (?:giu cho|dat cho|gui xe|sau khi " + ExplicitParkingBooking + @")\b")]
    private static partial Regex BookingReminder();
    [GeneratedRegex(@"\b(?:thong bao|notification)\b|\b(?:dat cho|booking)\b.*\b(?:thay bao|nhan tin)\b")]
    private static partial Regex Notice();
    [GeneratedRegex(@"\b(?:thong bao|notifications?) (?>(?:(?:ve|cho|for|about) )?)(?>(?:(?:a|the|my|this) )?)(?=\p{L})")]
    private static partial Regex NoticeQualifier();
    [GeneratedRegex(@"\A(?:(?:(?:viec |that )?(?:da |i )?(?:" + ParkingReference +
        @")|nhac dat cho|xac nhan dat cho|tu smartpark|tren smartpark|sau khi dat cho|after booking|sau khi " + ExplicitParkingBooking +
        @"|khong (?:toi|den) thi co phai (?:booking|dat cho)|thi|phai|what|where|how|chi|gi|dau|het|coi|xem|a)\b|" +
        NoticeTransactionFrame + ScopedTransaction + ")")]
    private static partial Regex NoticeScope();
    [GeneratedRegex(@"\b(?:(?:khong|chua) (?:thay|nhan|hien)|chua co|thong bao khong (?:toi|den)|chang thay|did not receive|didn t receive|missing)\b")]
    private static partial Regex MissingNotice();
}

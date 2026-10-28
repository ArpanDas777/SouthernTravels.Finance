using SouthernTravels.Core.Models;
using static SouthernTravels.Core.Infrastructure.Clock;
using SouthernTravels.Core.Infrastructure;
using SouthernTravels.Areas.Finance.Models;

namespace SouthernTravels.Areas.Finance.Data;

/// <summary>
/// The Finance report data. Two ways to get one:
/// <list type="bullet">
/// <item>the parameterless constructor seeds in-memory sample data (FinanceDemoService keeps one as a singleton and mutates it);</item>
/// <item>the list constructor holds a read-only snapshot loaded from the Finance API for one request (FinanceApiService).</item>
/// </list>
/// Vendor, ticket and KYC lists are index-aligned: <see cref="ServiceDef.Ti"/>/<see cref="ServiceDef.Vi"/> index <see cref="T"/>/<see cref="V"/>, and K[i] is V[i]'s KYC.
/// All mutations go through <see cref="Gate"/>.
/// </summary>
public sealed class FinanceStore
{

    public object Gate { get; } = new();

    public List<Vendor> V { get; }
    public List<Ticket> T { get; }
    public List<ServiceDef> S { get; }
    public List<Kyc> K { get; }

    public Dictionary<string, Bill> Bills { get; } = new();
    public List<BasketItem> Basket { get; }
    /// <summary>Payment Request: vendors with newly submitted tickets (red dot).</summary>
    public Dictionary<string, bool> Dots { get; } = new() { ["V101"] = true };
    /// <summary>Approved Vendor Payable: vendors with newly added bills (red dot).</summary>
    public Dictionary<string, bool> NewBills { get; } = new() { ["V101"] = true };
    public List<RtgsRecord> RtgsLog { get; } = [];
    /// <summary>Bulk Payment advices created from the Advance Report (same shape as an RTGS advice).</summary>
    public List<RtgsRecord> BulkLog { get; } = [];
    public List<AdvanceRequest> Adv { get; }

    /// <summary>A snapshot built from API data (see FinanceApiMapper).</summary>
    public FinanceStore(List<Vendor> v, List<Ticket> t, List<ServiceDef> s, List<Kyc> k, Dictionary<string, Bill> bills,
        List<BasketItem> basket, List<AdvanceRequest> adv, List<RtgsRecord> rtgs, List<RtgsRecord> bulk, IEnumerable<string> newTicketVendors, IEnumerable<string> newBillVendors)
    {
        V = v; T = t; S = s; K = k; Bills = bills; Basket = basket; Adv = adv; RtgsLog = rtgs; BulkLog = bulk;
        Dots = newTicketVendors.Distinct().ToDictionary(id => id, _ => true);
        NewBills = newBillVendors.Distinct().ToDictionary(id => id, _ => true);
    }

    /// <summary>No data: used when the API could not be reached, so the pages still render (with an error toast).</summary>
    public static FinanceStore Empty() => new([], [], [], [], new(), [], [], [], [], [], []);

    /// <summary>The service definition with this id.</summary>
    ServiceDef Def(string sid) => S.First(x => x.Id == sid);

    /// <summary>Seeds the in-memory sample data.</summary>
    public FinanceStore()
    {
        static string D4(string s) => "2026-" + s[..2] + "-" + s[2..];

        V = new (string, string, string, string, string)[]
        {
            ("V101","Hotel Residency Park","Accommodation","Kochi","Kerala"), ("V102","Sunrise Coaches","Transport","Bengaluru","Karnataka"),
            ("V103","Heritage Stays","Accommodation","Jaipur","Rajasthan"), ("V104","Anand Local Guides","Guide","Agra","Uttar Pradesh"),
            ("V105","Spice Route Caterers","En-Route Meals","Mysuru","Karnataka"), ("V106","Blue Lagoon Resorts","Accommodation","Port Blair","Andaman & Nicobar"),
            ("V107","Himalayan Cabs","Transport","Manali","Himachal Pradesh"), ("V108","Backwater Houseboats","Other","Alleppey","Kerala"),
            ("V109","Dhruv Tours & Guides","Guide","Varanasi","Uttar Pradesh"), ("V110","Gateway Hotels","Accommodation","Mumbai","Maharashtra"),
            ("V111","Royal Desert Safaris","Other","Jaisalmer","Rajasthan"), ("V112","Coastal Cabs","Transport","Goa","Goa"),
            ("V113","Perfect Holidays","Accommodation","Kochi","Kerala"), ("V114","Silver Oak Hospitality","Accommodation","Udaipur","Rajasthan"),
        }.Select(a => new Vendor(a.Item1, a.Item2, a.Item3, a.Item4, a.Item5)).ToList();

        var ex = new (string Name, string Branch, string Code)[]
        {
            ("S Sambhu Sankar", "VIZAG", "VIZ001"), ("D Yogita", "VIZAG", "VIZ001"), ("K Rajesh", "VIZAG", "VIZ001"),
            ("V N Babu", "RAJAHMUNDRY", "RAJ001"), ("P Rambabu", "RAJAHMUNDRY", "RAJ001"),
            ("VK Chaitanya", "VIJAYAWADA", "VIJ001"), ("G Kasi Rao", "VIJAYAWADA", "VIJ001"), ("Stanley Dayakar", "VIJAYAWADA", "VIJ001"),
            ("CH Vinod Kumar", "GUNTUR", "VIJ001"), ("P Gopi", "GUNTUR", "VIJ001"),
            ("Ajay Yerramekala", "NELLORE", "VIJ001"), ("Shaik Ikram Ahmed", "NELLORE", "VIJ001"),
            ("Gaurav", "DELHI", "DEL001"), ("Versha", "DELHI", "DEL001"), ("Ujjwal", "DELHI", "DEL001"),
            ("Chandu", "HYDERABAD", "HYD001"), ("Gopi", "HYDERABAD", "HYD001"), ("Jhansi", "HYDERABAD", "HYD001"),
        };

        var tk = new (string Id, string Pax, int N, int Ex, int Disc, double Frac, string Status)[]
        {
            ("GRP1001","Meera Krishnan",4,0,4000,1,"Confirmed"), ("SPL1002","Rohan Desai",2,1,0,1,"Confirmed"), ("GRP1003","Anita Verma",6,2,12000,.6,"Confirmed"),
            ("GRP1004","Kavita Iyer",3,0,2000,1,"Confirmed"), ("SPL1005","Imran Sheikh",2,3,1500,1,"Confirmed"), ("GRP1006","Lakshmi Narayanan",5,2,5000,.4,"Confirmed"),
            ("GRP1007","Deepak Joshi",4,1,0,1,"Confirmed"), ("SPL1008","Farah Khan",2,3,1000,1,"Pending"), ("GRP1009","Suresh Babu",8,0,15000,.6,"Confirmed"),
            ("SPL1010","Nisha Kapoor",2,1,0,1,"Confirmed"), ("GRP1011","Vikram Rathore",6,3,8000,1,"Confirmed"), ("GRP1012","Pooja Menon",4,0,0,.5,"Confirmed"),
            ("SPL1013","Gautam Reddy",2,2,2000,1,"Confirmed"), ("GRP1014","Sunita Agarwal",7,3,10000,1,"Confirmed"), ("SPL1015","Tarun Bhatia",3,1,0,1,"Confirmed"),
            ("GRP1016","Rekha Pillai",5,0,4000,.7,"Confirmed"), ("GRP1017","Harish Kulkarni",4,2,3000,1,"Confirmed"), ("SPL1018","Alka Sharma",2,3,0,1,"Confirmed"),
            ("GRP1019","Manoj Pandey",6,1,6000,1,"Confirmed"), ("GRP1020","Divya Nambiar",3,0,1000,1,"Confirmed"),
            ("GRP1021","Rajesh Malhotra",4,1,0,1,"Confirmed"),
        };

        // ticket, vendor, service, conf no., start (MMdd), end (MMdd), selling, contracted, negotiated, ops status, seeded bill (status + L1 + L2)
        var sv = new (int Ti, int Vi, string Name, string Conf, string S, string E, int Sell, int Contr, int Neg, char Ops, string? Seed)[]
        {
            (0,0,"Deluxe rooms, 5 nights","HRP-88121","0818","0823",54000,42000,41000,'C',"RAA"),
            (0,1,"Tempo traveller, 6 days","SC-4410","0818","0824",38000,29500,29500,'C',"RAA"),
            (0,7,"Houseboat overnight stay","BH-2231","0820","0821",22000,16000,15500,'C',"DAA"),
            (1,2,"Heritage suite, 3 nights","HS-7710","0825","0828",46000,36000,36000,'C',"RAA"),
            (1,3,"Full-day guide, Agra","AL-551","0826","0827",9000,6500,6800,'C',null),
            (2,5,"Beach resort, 4 nights","BL-3301","0901","0905",120000,96000,94000,'C',"RAA"),
            (2,1,"Coach transfers, 5 days","SC-4466","0901","0906",64000,50000,50000,'C',null),
            (2,4,"En-route meals, 5 days","SR-912","0901","0906",30000,21500,21500,'C',"RAA"),
            (3,0,"Superior rooms, 3 nights","HRP-88140","0910","0913",42000,33000,33500,'C',"RAA"),
            (3,7,"Houseboat day cruise","BH-2250","0912","0912",12000,8400,8400,'C',null),
            (4,9,"Executive rooms, 2 nights","GH-1190","0920","0922",34000,27000,27000,'C',null),
            (4,8,"Ghat walk guide","DT-208","0921","0921",4500,3000,3000,'C',"RAA"),
            (5,2,"Palace suite, 4 nights","HS-7744","1010","1014",98000,80000,79000,'I',null),
            (5,10,"Desert safari and camp","RD-310","1011","1012",41000,30000,30000,'I',null),
            (6,5,"Villa, 5 nights","BL-3320","0903","0908",82000,66000,66000,'C',"RAA"),
            (6,11,"Airport cabs","CC-77","0903","0908",16000,11000,11000,'C',"RAA"),
            (7,3,"Guide, 2 days","AL-560","1020","1022",9000,6000,6000,'N',null),
            (8,0,"Rooms, 6 nights","HRP-88170","0825","0831",122000,98000,98000,'C',"RAA"),
            (8,1,"Coach, 7 days","SC-4480","0825","0901",78000,60000,60000,'C',"RAA"),
            (8,4,"Meals, 6 days","SR-930","0825","0831",44000,31000,31000,'C',"DAA"),
            (9,6,"Cab, Manali to Kullu","HC-120","0915","0917",22000,16500,16500,'C',null),
            (10,2,"Haveli stay, 3 nights","HS-7760","1018","1021",70000,56000,56000,'I',null),
            (10,10,"Camel safari","RD-322","1019","1019",18000,12500,12500,'I',null),
            (11,7,"Houseboat, 2 nights","BH-2290","0905","0907",36000,26000,26000,'C',"RAA"),
            (11,0,"Rooms, 3 nights","HRP-88190","0905","0908",54000,42000,42000,'C',null),
            (12,5,"Resort, 3 nights","BL-3340","1005","1008",60000,48000,48000,'I',null),
            (12,1,"Transfer coach","","1005","1008",14000,10500,10500,'I',null),
            (13,9,"Rooms, 4 nights","GH-1230","0828","0901",96000,76000,76000,'C',"RAA"),
            (13,11,"Cab, 5 days","CC-90","0828","0902",32000,24000,24000,'C',"RAA"),
            (14,6,"Cab, Manali 4 days","HC-141","0830","0903",30000,22500,22500,'C',"RAA"),
            (15,0,"Rooms, 4 nights","HRP-88201","0912","0916",70000,55000,55000,'C',null),
            (15,3,"Guide, 2 days","AL-571","0913","0914",8000,5600,5600,'C',null),
            (16,2,"Suite, 3 nights","HS-7770","0907","0910",58000,46000,46800,'C',"RAA"),
            (16,4,"En-route meals","SR-950","0907","0910",20000,14000,14000,'C',"RAA"),
            (17,8,"Guide, Varanasi 2 days","DT-230","0922","0923",9500,6500,6500,'C',"RAA"),
            (18,5,"Resort, 5 nights","BL-3360","1112","1117",110000,88000,88000,'N',null),
            (18,11,"Airport and local cabs","CC-120","1112","1117",28000,20000,20000,'N',null),
            (19,9,"Rooms, 3 nights","GH-1260","0916","0919",52000,41000,41000,'C',"RAA"),
            (19,6,"Cab, 3 days","HC-170","0916","0919",16000,11500,11500,'C',"RAA"),
            (11,12,"Premium rooms, 3 nights","PH-1101","0905","0908",45000,36000,36000,'C',"DAA"),
            (11,12,"Local transfers, 3 days","PH-1102","0905","0908",12000,9000,9000,'C',"RAA"),
            (11,12,"Guide, 2 days","PH-1103","0905","0907",6000,4000,4000,'C',null),
            (11,12,"En-route meals, 3 days","PH-1104","0905","0908",9000,6500,6500,'C',"RAA"),
            (20,13,"Palace rooms, 2 nights","SO-2201","0910","0912",52000,40000,40000,'C',"RAA"),
            (20,13,"Airport and local transfers","SO-2202","0910","0912",14000,10000,10000,'C',"DAA"),
            (20,13,"Guide, 2 days","SO-2203","0910","0911",7000,5000,5000,'C',null),
            (20,13,"En-route meals, 2 days","SO-2204","0910","0912",10000,7000,7000,'C',"RAA"),
            (16,12,"Deluxe rooms, 4 nights","PH-1201","0927","1001",64000,50000,50000,'C',"RAA"),
            (16,12,"Transport, 5 days","PH-1202","0927","1001",30000,22000,22000,'C',"RAA"),
            (16,12,"Guide, 3 days","PH-1203","0928","1001",9000,6000,6000,'C',"RAA"),
            (16,12,"En-route meals, 5 days","PH-1204","0927","1001",15000,10500,10500,'C',"RAA"),
            (16,12,"Cultural evening show","PH-1205","0929","0929",8000,5500,5500,'C',"RAA"),
            (0,0,"Extra rooms, 2 nights","HRP-88301","0929","1001",26000,20000,20000,'C',"RAA"),
            (1,1,"Airport transfers","SC-4501","0930","1001",12000,8500,8500,'C',"RAA"),
            (2,2,"Garden suite, 2 nights","HS-7801","0929","1001",40000,31000,31000,'C',"RAA"),
            (3,3,"Heritage walk guide","AL-601","0930","0930",5000,3500,3500,'C',"RAA"),
            (4,4,"Farewell dinner","SR-990","1001","1001",9000,6500,6500,'C',"RAA"),
            (5,5,"Beach villa, 3 nights","BL-3401","0929","1002",54000,42000,42000,'C',"RAQ"),
            (6,6,"Valley cab, 3 days","HC-201","0930","1003",15000,11000,11000,'C',"RAQ"),
            (8,7,"Sunset cruise","BH-2301","1001","1001",10000,7000,7000,'C',"RAQ"),
            (9,8,"Temple guide","DT-301","1002","1002",4000,2800,2800,'C',"RAX"),
            (10,9,"Executive rooms, 2 nights","GH-1301","0930","1002",36000,28000,28000,'C',"RAA"),
            (11,10,"Dune dinner","RD-401","1001","1001",12000,8000,8000,'C',"RAA"),
            (13,11,"City cab, 2 days","CC-201","1002","1003",9000,6500,6500,'C',"RAA"),
        };
        var adv0 = new Dictionary<int, int> { [0] = 10000, [5] = 20000, [14] = 15000, [17] = 20000, [23] = 8000, [27] = 20000, [39] = 15000, [43] = 10000, [44] = 6000, [48] = 10000 };
        // One vendor can supply several service types; a hotel can have another name than its vendor.
        var stype = new Dictionary<int, string> { [39] = "Accommodation", [40] = "Transport", [41] = "Guide", [42] = "En-Route Meals",
            [43] = "Accommodation", [44] = "Transport", [45] = "Guide", [46] = "En-Route Meals",
            [47] = "Accommodation", [48] = "Transport", [49] = "Guide", [50] = "En-Route Meals", [51] = "Other" };
        var hotel = new Dictionary<int, string> { [39] = "Lakeview Grand Hotel", [43] = "Silver Oak Residency", [47] = "Lakeview Grand Hotel" };

        S = sv.Select((a, i) => new ServiceDef("S" + i, a.Ti, a.Vi, a.Name, a.Conf, D4(a.S), D4(a.E), a.Sell, a.Contr, a.Neg, a.Ops, a.Seed,
            adv0.GetValueOrDefault(i), stype.GetValueOrDefault(i), hotel.GetValueOrDefault(i))).ToList();

        T = tk.Select((a, i) =>
        {
            var sum = S.Where(s => s.Ti == i).Sum(s => s.Sell);
            int bas = Fmt.Round(sum * 1.08 / 100) * 100, gst = Fmt.Round(bas * .05 / 100) * 100, grand = bas - a.Disc + gst;
            var e = ex[(a.Ex * 5 + i) % ex.Length];
            var file = "FN/" + (1171 + i * 9).ToString("D6", Fmt.Inv) + "/2026-27/" + e.Code[..3] + (i + 1).ToString("D4", Fmt.Inv);
            return new Ticket(a.Id, "MBK" + (48201 + i * 13), file, a.Pax, a.N, e.Name, e.Branch,
                a.Disc, bas, gst, grand, Fmt.Round(grand * a.Frac / 100) * 100, a.Status, a.N >= 4 ? 1 : 0) { BranchCode = e.Code };
        }).ToList();

        for (int i = 0; i < S.Count; i++)
        {
            var s = S[i];
            if (s.Seed is null) continue;
            string dt = Fmt.Add(s.End, 2), no = "INV-2026-" + (400 + i * 7);
            bool disp = s.Seed[0] == 'D', below = !disp && i % 4 == 3;
            Approval L(char c, string who, int off) => c == 'A'
                ? new Approval { St = "A", By = who, At = Fmt.Add(dt, off) + " 15:10" }
                : c == 'X' ? new Approval { St = "X", By = "Mounika", At = Fmt.Add(dt, off) + " 16:00", Kind = "Rejected", Reason = "Guide invoice does not match the service report (sample)." }
                : new Approval { St = c.ToString() };
            Bills[s.Id] = new Bill
            {
                St = s.Seed[0].ToString(), Date = dt, No = no, Doc = "invoice_" + no + ".pdf", Voucher = "voucher_" + no + ".pdf",
                Inv = disp ? s.Neg + 2400 : below ? s.Neg - 1000 : s.Neg, Rec = disp ? Fmt.Round(s.Neg * .9 / 100) * 100 : below ? s.Neg - 1000 : s.Neg, Upl = dt + " 10:30",
                Mr = disp ? "Vendor billed extra charges not covered by the negotiated rate (sample reason)." : "",
                L1 = L(s.Seed[1], "Vijay Gupta", 1), L2 = L(s.Seed[2], i % 2 == 1 ? "Sowmya" : "Divya", 3),
            };
        }

        int[] dcount = [5, 5, 4, 5, 3, 5, 2, 5, 4, 5, 1, 3];
        string[] contacts = ["Ramesh Kumar", "Sunil Shetty", "Anjali Rawat", "Mohan Das", "Savitha B", "Joseph Fernandes", "Tenzin Dorje", "Biju Thomas", "Rajiv Mishra", "Neha Kulkarni", "Dilip Singh", "Carlos Pereira"];
        string[] gstState = ["32", "29", "08", "09", "29", "35", "02", "32", "09", "27", "08", "30"];
        string[] banks = ["HDFC Bank", "ICICI Bank", "State Bank of India", "Axis Bank", "Canara Bank", "HDFC Bank", "Punjab National Bank", "Federal Bank", "Bank of Baroda", "Kotak Mahindra Bank", "ICICI Bank", "HDFC Bank"];
        string[] ifsc = ["HDFC0001204", "ICIC0000412", "SBIN0003301", "UTIB0000118", "CNRB0000771", "HDFC0000532", "PUNB0112300", "FDRL0001002", "BARB0VARANA", "KKBK0000650", "ICIC0001266", "HDFC0000907"];
        K = V.Select((v, i) =>
        {
            var panCore = "AAB" + (char)(67 + i) + "P" + (1200 + i * 37) + "K";
            var email = new string(v.Name.ToLowerInvariant().Where(ch => ch is >= 'a' and <= 'z').ToArray());
            return new Kyc(i, Enumerable.Range(0, 5).Select(k => k < dcount[i % 12]).ToArray(), contacts[i % 12],
                "98" + (45120000 + i * 73519).ToString(Fmt.Inv)[..8], email[..Math.Min(12, email.Length)] + "@example.in",
                panCore, gstState[i % 12] + panCore + "1Z" + (i % 9 + 1), banks[i % 12], "5010" + (3300000 + i * 91117), ifsc[i % 12],
                i % 3 == 0 ? "2% / 15d" : i % 3 == 1 ? "1% / 30d" : "2% / 7d", i % 2 == 0 ? "Sowmya" : "Divya",
                dcount[i % 12] == 5 ? (i % 2 == 0 ? "Vijay Gupta" : "Sowmya") : "",
                i >= 7 ? Fmt.Add("2026-10-01", i - 7) : Fmt.Add("2025-03-04", i * 31), Fmt.Add("2026-11-15", (i - 5) * 24), i != 6 && i != 10,
                (12 + i * 5) + ", Main Road, " + v.City, "VKF-" + (2201 + i)) { Ap = MkKycDocs(i) };
        }).ToList();

        Basket =
        [
            new() { Sid = "S0", Pm = "RTGS" },
            new() { Sid = "S3", Pm = "E-Cheque" },
            new() { Sid = "S7", Pm = "RTGS", Paid = true, Mgmt = "Approved", By = "Mounika", At = "2026-10-02 12:20", Bank = "HDFC Bank", Mode = "NEFT", Utr = "N2026100231244",
                PaymentDate = "2026-10-02", AmountPaid = 21500, CType = "Physical Cheque", DetailsAt = "2026-10-02 10:00", ChequeNo = "CHQ448001" },
            // October entries: three RTGS without details, one E-Cheque with details and the UTR awaited, one paid with details and UTR.
            new() { Sid = "S52", Pm = "RTGS" },
            new() { Sid = "S53", Pm = "RTGS" },
            new() { Sid = "S54", Pm = "RTGS" },
            new() { Sid = "S55", Pm = "E-Cheque", CType = "E-Cheque", Mode = "NEFT", Bank = "CBI-Delhi", ChequeNo = "ERQ2610055", PaymentDate = "2026-10-12", DetailsAt = "2026-10-12 11:05" },
            new() { Sid = "S56", Pm = "RTGS", Paid = true, Mgmt = "Approved", By = "Mounika", At = "2026-10-13 15:30", CType = "Physical Cheque", Mode = "RTGS", Bank = "SBI-Hyderabad",
                ChequeNo = "CHQ448120", PaymentDate = "2026-10-13", DetailsAt = "2026-10-13 10:10", Utr = "UTR2026101301456", AmountPaid = 6500 },
        ];

        Adv =
        [
            new() { Sid = "S12", Amt = 30000, At = "2026-09-28 11:20", By = "S Sambhu Sankar", Br = "VIZ001", L1 = "A", L2 = "A" },
            new() { Sid = "S13", Amt = 12000, At = "2026-09-29 09:45", By = "S Sambhu Sankar", Br = "VIZ001", L1 = "A", L2 = "A" },
            new() { Sid = "S21", Amt = 20000, At = "2026-09-30 14:05", By = "VK Chaitanya", Br = "VIJ001", L1 = "A", L2 = "A" },
            new() { Sid = "S22", Amt = 5000, At = "2026-10-01 10:30", By = "VK Chaitanya", Br = "VIJ001", L1 = "A", L2 = "A" },
            new() { Sid = "S25", Amt = 15000, At = "2026-09-26 16:10", By = "S Sambhu Sankar", Br = "VIZ001", L1 = "A", L2 = "A",
                Pay = new() { Pct = 2, Tds = 300, Net = 14700, Date = "2026-10-03 11:42", CType = "E-Cheque", No = "ERQ2026100301", Utr = "UTR2026100301877", Bank = "HDFC Bank", Mode = "NEFT",
                    PaymentDate = "2026-10-03", AmountPaid = 14700 } },
            new() { Sid = "S26", Amt = 3000, At = "2026-10-02 12:00", By = "S Sambhu Sankar", Br = "VIZ001", L1 = "X", L2 = "-",
                Rej = new("L1", "Advance not justified — service starts more than 30 days from now.", "Mounika", "2026-10-02 15:10") },
            new() { Sid = "S35", Amt = 25000, At = "2026-10-03 17:25", By = "VK Chaitanya", Br = "VIJ001", L1 = "A", L2 = "A" },
            new() { Sid = "S36", Amt = 8000, At = "2026-10-03 17:40", By = "VK Chaitanya", Br = "VIJ001", L1 = "A", L2 = "A" },
// Waiting for the approver: two with L1 approved (L2 awaiting), one awaiting L1, one rejected by L2.
            new() { Sid = "S29", Amt = 9000, At = "2026-10-20 10:15", By = "S Sambhu Sankar", Br = "VIZ001", L1 = "A", L2 = "Q" },
            new() { Sid = "S30", Amt = 6000, At = "2026-10-21 12:40", By = "VK Chaitanya", Br = "VIJ001", L1 = "A", L2 = "Q" },
            new() { Sid = "S31", Amt = 4000, At = "2026-10-22 09:30", By = "VK Chaitanya", Br = "VIJ001", L1 = "Q", L2 = "-" },
            new() { Sid = "S32", Amt = 5000, At = "2026-10-23 16:05", By = "S Sambhu Sankar", Br = "VIZ001", L1 = "A", L2 = "X", L2By = "Vijay Gupta", L2At = "2026-10-24 11:00",
                Rej = new("L2", "Vendor quote is still being negotiated (sample).", "Vijay Gupta", "2026-10-24 11:00") },
        ];
    }


    /// <summary>All services joined with ticket, vendor, bill, basket and advance state.</summary>
    public List<Svc> Svcs()
    {
        lock (Gate)
        {
            return S.Select(s =>
            {
                var bk = Basket.FirstOrDefault(b => b.Sid == s.Id);
                var paid = Adv.Where(a => a.Sid == s.Id && a.Paid).ToList();
                Bills.TryGetValue(s.Id, out var bill);
                var v = V[s.Vi];
                return new Svc
                {
                    Def = s, T = T[s.Ti], V = s.SType is null ? v : v with { Type = s.SType }, Bill = bill, Bk = bk,
                    AdvPaid = bk is { Paid: true } ? s.Neg : s.Adv0 + paid.Sum(a => a.Amt),
                    AdvCount = (s.Adv0 > 0 ? 1 : 0) + paid.Count,
                    Eligible = bill == null,
                };
            }).ToList();
        }
    }

    /// <summary>Billed services still waiting to be submitted to the payment basket.</summary>
    public List<Svc> AwaitingSubmission(List<Svc> all)
    {
        var inB = Basket.Select(b => b.Sid).ToHashSet();
        return all.Where(s => s.BStat == "R" && s.Ops != 'N' && s.T.Status != "Pending" && !inB.Contains(s.Id)).ToList();
    }

    /* ---------- mutations ---------- */

    /// <summary>Saves raised bills; each vendor gets the "new bill(s) added" dot in Approved Vendor Payable.</summary>
    public void RaiseBills(IEnumerable<(string Sid, Bill Bill)> bills)
    {
        lock (Gate)
        {
            foreach (var (sid, bill) in bills)
            {
                if (Bills.TryGetValue(sid, out var old)) bill.Hist = [.. old.Hist, old];   // a reupload keeps the version it replaces
                Bills[sid] = bill;
                NewBills[V[Def(sid).Vi].Id] = true;
            }
        }
    }

    /// <summary>Sample KYC document states (placeholder): a mix of System Approved, Approved, Pending, Rejected and Not uploaded; vendor 0 is complete.</summary>
    static List<KycDoc> MkKycDocs(int i) => KycDocs.All.Select((d, k) =>
    {
        var n = (i * 7 + k * 3) % 10;
        var st = i == 0 ? (d.Via != null ? "V" : "A")
            : n < 4 ? (d.Via != null ? "V" : "A") : n < 7 ? "P" : n == 7 ? (i % 4 == 1 ? "R" : "P") : n == 8 ? (i % 3 == 0 ? "N" : "A") : "A";
        var doc = new KycDoc { St = st };
        if (st == "A") { doc.By = "Mounika"; doc.At = "2026-10-0" + (1 + (i + k) % 5) + " 11:00"; }
        if (st == "R") { doc.By = "Mounika"; doc.At = "2026-10-03 16:20"; doc.Reason = "Document is not legible (sample)."; }
        return doc;
    }).ToList();

    /// <summary>The approver approves (status A) or rejects (status X, with a reason) one document of a vendor.</summary>
    public void SetKycDoc(int vendorIndex, string key, string status, string? reason, string by)
    {
        lock (Gate)
        {
            var k = Array.FindIndex(KycDocs.All, d => d.Key == key);
            if (k < 0 || vendorIndex < 0 || vendorIndex >= K.Count || K[vendorIndex].Ap.Count <= k) return;
            var doc = K[vendorIndex].Ap[k];
            doc.St = status == "A" ? "A" : "R"; doc.By = by; doc.At = NowStamp(); doc.Reason = status == "A" ? null : reason;
        }
    }

    /// <summary>The approver (L2) approves or rejects an advance request that L1 has approved.</summary>
    public bool DecideAdvance(string sid, bool approve, string by, string? reason = null)
    {
        lock (Gate)
        {
            var a = Adv.FirstOrDefault(x => x.Sid == sid);
            if (a is not { L1: "A" } || a.L2 is "A" or "X") return false;
            a.L2 = approve ? "A" : "X"; a.L2By = by; a.L2At = NowStamp();
            if (!approve) a.Rej = new AdvRej("L2", reason, by, a.L2At);
            return true;
        }
    }

    /// <summary>The approver (L2) approves a bill that L1 has approved, or rejects it / marks it as disputed (<paramref name="kind"/>) with a reason.</summary>
    public bool DecideBill(string sid, bool approve, string by, string? kind = null, string? reason = null)
    {
        lock (Gate)
        {
            if (!Bills.TryGetValue(sid, out var b) || b.L1.St != "A" || (approve && b.L2.St == "A")) return false;
            b.L2 = new Approval { St = approve ? "A" : "X", By = by, At = NowStamp(), Kind = approve ? null : kind, Reason = approve ? null : reason };
            return true;
        }
    }

    /// <summary>Moves services into the payment basket; vendors that already had items get the "new tickets" dot.</summary>
    /// <param name="modes">Planned payment mode per vendor index (from the advice); RTGS when absent.</param>
    public int Submit(IReadOnlyCollection<string> ids, IReadOnlyDictionary<int, string> modes)
    {
        lock (Gate)
        {
            var sel = AwaitingSubmission(Svcs()).Where(s => ids.Contains(s.Id)).ToList();
            var had = Basket.Select(b => Def(b.Sid).Vi).ToHashSet();
            foreach (var vi in sel.Select(s => s.Vi).Distinct())
                if (had.Contains(vi)) Dots[V[vi].Id] = true;
            Basket.AddRange(sel.Select(s => new BasketItem { Sid = s.Id, Pm = modes.GetValueOrDefault(s.Vi, "RTGS") }));
            return sel.Count;
        }
    }

    /// <summary>The approved payable of a service (the recommended amount of its bill).</summary>
    int Payable(string sid) => Bills.TryGetValue(sid, out var b) ? b.Rec : 0;

    /// <summary>Unpaid basket entries planned for RTGS that no RTGS advice covers yet.</summary>
    public List<BasketItem> RtgsPool()
    {
        lock (Gate) return Basket.Where(b => !b.Paid && (b.Pm ?? "RTGS") == "RTGS" && b.RtgsNo == null).ToList();
    }

    /// <summary>
    /// Creates an RTGS advice for the chosen entries (only those still in the RTGS pool). The payment details and each entry's TDS % are saved on the entries
    /// (no UTR yet)
    /// </summary>
    public RtgsRecord? CreateRtgs(IReadOnlyDictionary<string, decimal> tdsPct, RtgsCommon common, string by)
    {
        lock (Gate)
        {
            var chosen = RtgsPool().Where(b => tdsPct.ContainsKey(b.Sid)).ToList();
            if (chosen.Count == 0) return null;
            var rec = new RtgsRecord { No = "RTGS-2610-" + (201 + RtgsLog.Count), At = NowStamp(), By = by, Sids = chosen.Select(b => b.Sid).ToList(), Common = common };
            foreach (var b in chosen)
            {
                b.RtgsNo = rec.No; b.Pct = tdsPct[b.Sid]; b.Tds = Fmt.Round((double)(Payable(b.Sid) * b.Pct / 100));
                b.CType = common.CType; b.Mode = common.Mode; b.Bank = common.SenderBank; b.ChequeNo = common.ChequeNo; b.PaymentDate = common.ChequeDate;
                b.Rnd = common.RandomNo; b.DetailsAt = NowStamp();
            }
            RtgsLog.Add(rec);
            return rec;
        }
    }

    /// <summary>
    /// Payment Request payment of one entry. Step 1 saves the details (no UTR); step 2 (UTR given) marks it Paid.
    /// <paramref name="net"/> is the amount paid once the UTR is in.
    /// </summary>
    public void SavePayDetails(string sid, string by, string cType, string mode, string bank, string chequeNo, string date, string? utr, int net)
    {
        lock (Gate)
        {
            var b = Basket.First(x => x.Sid == sid);
            b.CType = cType; b.Mode = mode; b.Bank = bank; b.ChequeNo = chequeNo; b.PaymentDate = date; b.DetailsAt ??= NowStamp();
            if (!string.IsNullOrWhiteSpace(utr)) { b.Utr = utr; b.Paid = true; b.Mgmt = "Approved"; b.By = by; b.At = NowStamp(); b.AmountPaid = net; }
        }
    }

    /// <summary>Update Details of an RTGS advice: a UTR per entry; an entry with a UTR becomes Paid. Returns how many were marked paid.</summary>
    public int SaveRtgsUtrs(string no, string by, IReadOnlyDictionary<string, string> utrs)
    {
        lock (Gate)
        {
            var n = 0;
            foreach (var b in Basket.Where(x => x.RtgsNo == no && !x.Paid && utrs.GetValueOrDefault(x.Sid, "") != ""))
            {
                b.Utr = utrs[b.Sid]; b.Paid = true; b.Mgmt = "Approved"; b.By = by; b.At = NowStamp(); b.AmountPaid = Payable(b.Sid) - b.Tds; n++;
            }
            return n;
        }
    }

    /// <summary>
    /// Saves an advance's payment details (step 1 has no UTR; step 2 adds it, which makes the advance Paid).
    /// TDS/net are derived from the %; the "added" stamp is kept from step 1.
    /// </summary>
    public void PayAdvance(string sid, AdvancePayment pay)
    {
        lock (Gate)
        {
            var a = Adv.First(x => x.Sid == sid);
            pay.Settle(a.Amt);
            pay.Date = a.Pay?.Date ?? NowStamp();
            pay.BulkNo ??= a.Pay?.BulkNo;
            pay.Rnd = pay.Rnd != "" ? pay.Rnd : a.Pay?.Rnd ?? "";
            pay.AmountPaid = pay.Paid ? pay.Net : 0;
            a.Pay = pay;
        }
    }

    /// <summary>Advances that are approved and have no payment details yet (the ones a Bulk Payment can take).</summary>
    public List<AdvanceRequest> BulkPool()
    {
        lock (Gate) return Adv.Where(a => a.Approved && a.Pay == null).ToList();
    }

    /// <summary>
    /// Bulk Payment: saves the shared payment details on every chosen advance (TDS % per advance; no UTR yet) and logs a Bulk Payment advice.
    /// </summary>
    public RtgsRecord? CreateBulk(IReadOnlyDictionary<string, decimal> tdsPct, AdvancePayment common, RtgsCommon advice, string by)
    {
        lock (Gate)
        {
            var chosen = BulkPool().Where(a => tdsPct.ContainsKey(a.Sid)).ToList();
            if (chosen.Count == 0) return null;
            var rec = new RtgsRecord { No = "BPA-2610-" + (201 + BulkLog.Count), At = NowStamp(), By = by, Sids = chosen.Select(a => a.Sid).ToList(), Common = advice };
            foreach (var a in chosen)
            {
                var p = new AdvancePayment
                {
                    Pct = tdsPct[a.Sid], CType = common.CType, No = common.No, Bank = common.Bank, Mode = common.Mode, PaymentDate = common.PaymentDate,
                    Rnd = common.Rnd, BulkNo = rec.No, Date = NowStamp(),
                };
                p.Settle(a.Amt);
                a.Pay = p;
            }
            BulkLog.Add(rec);
            return rec;
        }
    }

    /// <summary>Update UTR of a Bulk Payment advice: a UTR per advance; an advance with a UTR becomes Paid. Returns how many were marked paid.</summary>
    public int SaveBulkUtrs(string bulkNo, IReadOnlyDictionary<string, string> utrs)
    {
        lock (Gate)
        {
            var n = 0;
            foreach (var a in Adv.Where(x => x.Pay?.BulkNo == bulkNo && !x.Paid && utrs.GetValueOrDefault(x.Sid, "") != ""))
            {
                a.Pay!.Utr = utrs[a.Sid]; a.Pay.AmountPaid = a.Pay.Net; n++;
            }
            return n;
        }
    }

    /// <summary>Changes the planned payment mode of unpaid entries not yet covered by an RTGS advice; returns how many changed.</summary>
    public int SetPlanMode(IEnumerable<string> sids, string mode)
    {
        lock (Gate)
        {
            var set = sids.ToHashSet();
            var open = Basket.Where(b => set.Contains(b.Sid) && !b.Paid && b.RtgsNo == null).ToList();
            foreach (var b in open) b.Pm = mode;
            return open.Count;
        }
    }

    /// <param name="area">"pr" (Payment Request) or "avp" (Approved Vendor Payable).</param>
    public void MarkSeen(string area, string vendorId)
    {
        lock (Gate) (area == "avp" ? NewBills : Dots)[vendorId] = false;
    }
}

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
    public List<AdvanceRequest> Adv { get; }

    /// <summary>A snapshot built from API data (see FinanceApiMapper).</summary>
    public FinanceStore(List<Vendor> v, List<Ticket> t, List<ServiceDef> s, List<Kyc> k, Dictionary<string, Bill> bills,
        List<BasketItem> basket, List<AdvanceRequest> adv, List<RtgsRecord> rtgs, IEnumerable<string> newTicketVendors, IEnumerable<string> newBillVendors)
    {
        V = v; T = t; S = s; K = k; Bills = bills; Basket = basket; Adv = adv; RtgsLog = rtgs;
        Dots = newTicketVendors.Distinct().ToDictionary(id => id, _ => true);
        NewBills = newBillVendors.Distinct().ToDictionary(id => id, _ => true);
    }

    /// <summary>No data: used when the API could not be reached, so the pages still render (with an error toast).</summary>
    public static FinanceStore Empty() => new([], [], [], [], new(), [], [], [], [], []);

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
        }.Select(a => new Vendor(a.Item1, a.Item2, a.Item3, a.Item4, a.Item5)).ToList();

        var ex = new[] { ("Priya Nair", "Kochi"), ("Rahul Mehta", "Mumbai"), ("Sneha Rao", "Bengaluru"), ("Arjun Singh", "Delhi") };

        var tk = new (string Id, string Pax, int N, int Ex, int Disc, double Frac, string Status)[]
        {
            ("GRP-1001","Meera Krishnan",4,0,4000,1,"Confirmed"), ("SPL-1002","Rohan Desai",2,1,0,1,"Confirmed"), ("GRP-1003","Anita Verma",6,2,12000,.6,"Confirmed"),
            ("GRP-1004","Kavita Iyer",3,0,2000,1,"Confirmed"), ("SPL-1005","Imran Sheikh",2,3,1500,1,"Confirmed"), ("GRP-1006","Lakshmi Narayanan",5,2,5000,.4,"Confirmed"),
            ("GRP-1007","Deepak Joshi",4,1,0,1,"Confirmed"), ("SPL-1008","Farah Khan",2,3,1000,1,"Pending"), ("GRP-1009","Suresh Babu",8,0,15000,.6,"Confirmed"),
            ("SPL-1010","Nisha Kapoor",2,1,0,1,"Confirmed"), ("GRP-1011","Vikram Rathore",6,3,8000,1,"Confirmed"), ("GRP-1012","Pooja Menon",4,0,0,.5,"Confirmed"),
            ("SPL-1013","Gautam Reddy",2,2,2000,1,"Confirmed"), ("GRP-1014","Sunita Agarwal",7,3,10000,1,"Confirmed"), ("SPL-1015","Tarun Bhatia",3,1,0,1,"Confirmed"),
            ("GRP-1016","Rekha Pillai",5,0,4000,.7,"Confirmed"), ("GRP-1017","Harish Kulkarni",4,2,3000,1,"Confirmed"), ("SPL-1018","Alka Sharma",2,3,0,1,"Confirmed"),
            ("GRP-1019","Manoj Pandey",6,1,6000,1,"Confirmed"), ("GRP-1020","Divya Nambiar",3,0,1000,1,"Confirmed"),
        };

        // ticket, vendor, service, conf no., start (MMdd), end (MMdd), selling, contracted, negotiated, ops status, seeded bill (status + L1 + L2)
        var sv = new (int Ti, int Vi, string Name, string Conf, string S, string E, int Sell, int Contr, int Neg, char Ops, string? Seed)[]
        {
            (0,0,"Deluxe rooms, 5 nights","HRP-88121","0818","0823",54000,42000,41000,'C',"RAA"),
            (0,1,"Tempo traveller, 6 days","SC-4410","0818","0824",38000,29500,29500,'C',"RAQ"),
            (0,7,"Houseboat overnight stay","BH-2231","0820","0821",22000,16000,15500,'C',"D--"),
            (1,2,"Heritage suite, 3 nights","HS-7710","0825","0828",46000,36000,36000,'C',"RAA"),
            (1,3,"Full-day guide, Agra","AL-551","0826","0827",9000,6500,6800,'C',null),
            (2,5,"Beach resort, 4 nights","BL-3301","0901","0905",120000,96000,94000,'C',"RQ-"),
            (2,1,"Coach transfers, 5 days","SC-4466","0901","0906",64000,50000,50000,'C',null),
            (2,4,"En-route meals, 5 days","SR-912","0901","0906",30000,21500,21500,'C',"RAA"),
            (3,0,"Superior rooms, 3 nights","HRP-88140","0910","0913",42000,33000,33500,'C',"RAX"),
            (3,7,"Houseboat day cruise","BH-2250","0912","0912",12000,8400,8400,'C',null),
            (4,9,"Executive rooms, 2 nights","GH-1190","0920","0922",34000,27000,27000,'C',null),
            (4,8,"Ghat walk guide","DT-208","0921","0921",4500,3000,3000,'C',"RAA"),
            (5,2,"Palace suite, 4 nights","HS-7744","1010","1014",98000,80000,79000,'I',null),
            (5,10,"Desert safari and camp","RD-310","1011","1012",41000,30000,30000,'I',null),
            (6,5,"Villa, 5 nights","BL-3320","0903","0908",82000,66000,66000,'C',"RAA"),
            (6,11,"Airport cabs","CC-77","0903","0908",16000,11000,11000,'C',"RQ-"),
            (7,3,"Guide, 2 days","AL-560","1020","1022",9000,6000,6000,'N',null),
            (8,0,"Rooms, 6 nights","HRP-88170","0825","0831",122000,98000,98000,'C',"RAA"),
            (8,1,"Coach, 7 days","SC-4480","0825","0901",78000,60000,60000,'C',"RAA"),
            (8,4,"Meals, 6 days","SR-930","0825","0831",44000,31000,31000,'C',"DQ-"),
            (9,6,"Cab, Manali to Kullu","HC-120","0915","0917",22000,16500,16500,'C',null),
            (10,2,"Haveli stay, 3 nights","HS-7760","1018","1021",70000,56000,56000,'I',null),
            (10,10,"Camel safari","RD-322","1019","1019",18000,12500,12500,'I',null),
            (11,7,"Houseboat, 2 nights","BH-2290","0905","0907",36000,26000,26000,'C',"RAA"),
            (11,0,"Rooms, 3 nights","HRP-88190","0905","0908",54000,42000,42000,'C',null),
            (12,5,"Resort, 3 nights","BL-3340","1005","1008",60000,48000,48000,'I',null),
            (12,1,"Transfer coach","","1005","1008",14000,10500,10500,'I',null),
            (13,9,"Rooms, 4 nights","GH-1230","0828","0901",96000,76000,76000,'C',"RAA"),
            (13,11,"Cab, 5 days","CC-90","0828","0902",32000,24000,24000,'C',"RAQ"),
            (14,6,"Cab, Manali 4 days","HC-141","0830","0903",30000,22500,22500,'C',"RAA"),
            (15,0,"Rooms, 4 nights","HRP-88201","0912","0916",70000,55000,55000,'C',null),
            (15,3,"Guide, 2 days","AL-571","0913","0914",8000,5600,5600,'C',null),
            (16,2,"Suite, 3 nights","HS-7770","0907","0910",58000,46000,46800,'C',"RAA"),
            (16,4,"En-route meals","SR-950","0907","0910",20000,14000,14000,'C',"RAA"),
            (17,8,"Guide, Varanasi 2 days","DT-230","0922","0923",9500,6500,6500,'C',"RQ-"),
            (18,5,"Resort, 5 nights","BL-3360","1112","1117",110000,88000,88000,'N',null),
            (18,11,"Airport and local cabs","CC-120","1112","1117",28000,20000,20000,'N',null),
            (19,9,"Rooms, 3 nights","GH-1260","0916","0919",52000,41000,41000,'C',"RAA"),
            (19,6,"Cab, 3 days","HC-170","0916","0919",16000,11500,11500,'C',"RAA"),
        };
        var adv0 = new Dictionary<int, int> { [0] = 10000, [5] = 20000, [14] = 15000, [17] = 20000, [23] = 8000, [27] = 20000 };

        S = sv.Select((a, i) => new ServiceDef("S" + i, a.Ti, a.Vi, a.Name, a.Conf, D4(a.S), D4(a.E), a.Sell, a.Contr, a.Neg, a.Ops, a.Seed,
            adv0.GetValueOrDefault(i))).ToList();

        T = tk.Select((a, i) =>
        {
            var sum = S.Where(s => s.Ti == i).Sum(s => s.Sell);
            int bas = Fmt.Round(sum * 1.08 / 100) * 100, gst = Fmt.Round(bas * .05 / 100) * 100, grand = bas - a.Disc + gst;
            return new Ticket(a.Id, "MBK-" + (48201 + i * 13), "F-" + (7731 + i * 9), a.Pax, a.N, ex[a.Ex].Item1, ex[a.Ex].Item2,
                a.Disc, bas, gst, grand, Fmt.Round(grand * a.Frac / 100) * 100, a.Status, a.N >= 6 ? 2 : a.N >= 4 ? 1 : 0);
        }).ToList();

        for (int i = 0; i < S.Count; i++)
        {
            var s = S[i];
            if (s.Seed is null) continue;
            string dt = Fmt.Add(s.End, 2), no = "INV-2026-" + (400 + i * 7);
            bool disp = s.Seed[0] == 'D';
            Approval L(char c, string who, int off) => c == 'A'
                ? new Approval { St = "A", By = who, At = Fmt.Add(dt, off) + " 15:10" }
                : new Approval { St = c.ToString() };
            Bills[s.Id] = new Bill
            {
                St = s.Seed[0].ToString(), Date = dt, No = no, Doc = "invoice_" + no + ".pdf", Voucher = "voucher_" + no + ".pdf",
                Inv = disp ? s.Neg + 2400 : s.Neg, Rec = disp ? Fmt.Round(s.Neg * .9 / 100) * 100 : s.Neg, Upl = dt + " 10:30",
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
            return new Kyc(i, Enumerable.Range(0, 5).Select(k => k < dcount[i]).ToArray(), contacts[i],
                "98" + (45120000 + i * 73519).ToString(Fmt.Inv)[..8], email[..Math.Min(12, email.Length)] + "@example.in",
                panCore, gstState[i] + panCore + "1Z" + (i % 9 + 1), banks[i], "5010" + (3300000 + i * 91117), ifsc[i],
                i % 3 == 0 ? "2% / 15d" : i % 3 == 1 ? "1% / 30d" : "2% / 7d", i % 2 == 0 ? "Sowmya" : "Divya",
                dcount[i] == 5 ? (i % 2 == 0 ? "Vijay Gupta" : "Sowmya") : "",
                Fmt.Add("2025-03-04", i * 31), Fmt.Add("2026-11-15", (i - 5) * 24), i != 6 && i != 10,
                (12 + i * 5) + ", Main Road, " + v.City, "VKF-" + (2201 + i));
        }).ToList();

        Basket =
        [
            new() { Sid = "S0", Pm = "RTGS" },
            new() { Sid = "S3", Pm = "E-Cheque" },
            new() { Sid = "S7", Pm = "RTGS", Paid = true, Mgmt = "Approved", By = "Mounika", At = "2026-10-02 12:20", Bank = "HDFC Bank", Mode = "NEFT", Utr = "N2026100231244",
                PaymentDate = "2026-10-02", AmountPaid = 21500, Slip = "payment_slip_N2026100231244.pdf" },
        ];

        Adv =
        [
            new() { Sid = "S12", Amt = 30000, At = "2026-09-28 11:20", By = "Karthik R", Br = "BLR", L1 = "A", L2 = "A" },
            new() { Sid = "S13", Amt = 12000, At = "2026-09-29 09:45", By = "Karthik R", Br = "BLR", L1 = "A", L2 = "Q" },
            new() { Sid = "S21", Amt = 20000, At = "2026-09-30 14:05", By = "Divya S", Br = "DEL", L1 = "A", L2 = "A" },
            new() { Sid = "S22", Amt = 5000, At = "2026-10-01 10:30", By = "Divya S", Br = "DEL", L1 = "Q", L2 = "-" },
            new() { Sid = "S25", Amt = 15000, At = "2026-09-26 16:10", By = "Karthik R", Br = "BLR", L1 = "A", L2 = "A",
                Pay = new() { Pct = 2, Tds = 300, Net = 14700, Date = "2026-10-03 11:42", CType = "Online", No = "UTR2026100301877", Bank = "HDFC Bank", Mode = "NEFT",
                    PaymentDate = "2026-10-03", AmountPaid = 14700, Slip = "payment_slip_UTR2026100301877.pdf" } },
            new() { Sid = "S26", Amt = 3000, At = "2026-10-02 12:00", By = "Karthik R", Br = "BLR", L1 = "X", L2 = "-" },
            new() { Sid = "S35", Amt = 25000, At = "2026-10-03 17:25", By = "Divya S", Br = "DEL", L1 = "Q", L2 = "-" },
            new() { Sid = "S36", Amt = 8000, At = "2026-10-03 17:40", By = "Divya S", Br = "DEL", L1 = "A", L2 = "A" },
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
                var ad = Adv.FirstOrDefault(a => a.Sid == s.Id && a.Pay != null);
                Bills.TryGetValue(s.Id, out var bill);
                return new Svc
                {
                    Def = s, T = T[s.Ti], V = V[s.Vi], Bill = bill, Bk = bk,
                    AdvPaid = bk is { Paid: true } ? s.Neg : s.Adv0 + (ad?.Amt ?? 0),
                    Eligible = bill == null && Fmt.Cmp(s.End, Today) < 0,
                };
            }).ToList();
        }
    }

    /// <summary>Billed services still waiting to be submitted to the payment basket.</summary>
    public List<Svc> AwaitingSubmission(List<Svc> all)
    {
        var inB = Basket.Select(b => b.Sid).ToHashSet();
        return all.Where(s => s.Bill != null && s.Ops != 'N' && s.T.Status != "Pending" && !inB.Contains(s.Id)).ToList();
    }

    /* ---------- mutations ---------- */

    /// <summary>Saves raised bills; each vendor gets the "new bill(s) added" dot in Approved Vendor Payable.</summary>
    public void RaiseBills(IEnumerable<(string Sid, Bill Bill)> bills)
    {
        lock (Gate)
        {
            foreach (var (sid, bill) in bills)
            {
                Bills[sid] = bill;
                NewBills[V[Def(sid).Vi].Id] = true;
            }
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

    /// <summary>Unpaid basket entries planned for RTGS that no RTGS advice covers yet.</summary>
    public List<BasketItem> RtgsPool()
    {
        lock (Gate) return Basket.Where(b => !b.Paid && (b.Pm ?? "RTGS") == "RTGS" && b.RtgsNo == null).ToList();
    }

    /// <summary>Creates an RTGS advice for the given services (only those still in the RTGS pool).</summary>
    public RtgsRecord? CreateRtgs(IReadOnlyCollection<string> sids, RtgsCommon common, string by)
    {
        lock (Gate)
        {
            var chosen = RtgsPool().Where(b => sids.Contains(b.Sid)).ToList();
            if (chosen.Count == 0) return null;
            var rec = new RtgsRecord { No = "RTGS-2610-" + (201 + RtgsLog.Count), At = NowStamp(), By = by, Sids = chosen.Select(b => b.Sid).ToList(), Common = common };
            foreach (var b in chosen) b.RtgsNo = rec.No;
            RtgsLog.Add(rec);
            return rec;
        }
    }

    /// <summary>Records an advance payment. TDS/net are derived from the %; the "added" stamp is set here.</summary>
    public void PayAdvance(string sid, AdvancePayment pay)
    {
        lock (Gate)
        {
            var a = Adv.First(x => x.Sid == sid);
            pay.Settle(a.Amt);
            pay.Date = NowStamp();
            a.Pay = pay;
        }
    }

    /// <summary>Records a payment-request payment. The "added" stamp and payer are set here.</summary>
    public void PayRequest(string sid, string by, string bank, string mode, string? utr, string? chequeNo, string paymentDate, int amountPaid, string slip)
    {
        lock (Gate)
        {
            var b = Basket.First(x => x.Sid == sid);
            b.Paid = true; b.Mgmt = "Approved"; b.By = by; b.At = NowStamp(); b.Bank = bank; b.Mode = mode; b.Utr = utr;
            b.ChequeNo = chequeNo; b.PaymentDate = paymentDate; b.AmountPaid = amountPaid; b.Slip = slip;
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

    /// <summary>Records one payment against every unpaid ticket of an RTGS advice; each ticket is paid its own approved payable.</summary>
    public int PayRtgs(string no, string by, string bank, string mode, string? utr, string? chequeNo, string paymentDate, string slip, Func<string, int> payable)
    {
        lock (Gate)
        {
            var n = 0;
            foreach (var b in Basket.Where(x => x.RtgsNo == no && !x.Paid))
            {
                b.Paid = true; b.Mgmt = "Approved"; b.By = by; b.At = NowStamp(); b.Bank = bank; b.Mode = mode; b.Utr = utr;
                b.ChequeNo = chequeNo; b.PaymentDate = paymentDate; b.AmountPaid = payable(b.Sid); b.Slip = slip; n++;
            }
            return n;
        }
    }

    /// <param name="area">"pr" (Payment Request) or "avp" (Approved Vendor Payable).</param>
    public void MarkSeen(string area, string vendorId)
    {
        lock (Gate) (area == "avp" ? NewBills : Dots)[vendorId] = false;
    }
}

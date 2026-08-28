#nullable disable
using System;
using System.Collections.Generic;

namespace BaggageDelivery.Core.Models;

public partial class TucManualMessage
{
    public int UcmmId { get; set; }

    public DateTime UcmmDate { get; set; }

    public int? UcmmSendTo { get; set; }

    public int? UcmmStaffId { get; set; }

    public string UcmmWindowsUser { get; set; }

    public string UcmmMessage { get; set; }

    public bool UcmmSent { get; set; }

    public DateTime? UcmmTimeSent { get; set; }

    public int? UcmmAttempts { get; set; }

    public string SendToEmailAddress { get; set; }

    public string SendToMobile { get; set; }

    public string Subject { get; set; }

    public string ReplyToEmailAddress { get; set; }

    public int? JobId { get; set; }

    public bool Read { get; set; }

    public DateTime? TimeRead { get; set; }

    public bool? HasAttachment { get; set; }

    public bool? IsExternalStorage { get; set; }

    public string FilePath { get; set; }

    public string FileName { get; set; }

    public string FileType { get; set; }

    public byte[] FileContent { get; set; }

    public int? UcmmSendToCourierId { get; set; }

    public int? UcmmSendToStaffId { get; set; }

    public int? UcmmSendFromCourierId { get; set; }

    public int? UcmmSendFromStaffId { get; set; }
}
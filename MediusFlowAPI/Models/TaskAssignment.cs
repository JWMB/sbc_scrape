using System;
using System.Collections.Generic;
using System.Text;

namespace MediusFlowAPI.Models
{
	public class TaskAssignment
	{
		//[{"$type":"Medius.Core.Services.TaskAssignmentInfo, Medius.Core.Common","TaskId":10551609,"TaskDescription":"Arkiverad","Assignee":null}]

		public long TaskId { get; set; }

		public string TaskDescription { get; set; }
		//Assignee
	}
}

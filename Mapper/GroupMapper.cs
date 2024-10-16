
using AutoMapper;
using Splitwise_Back.Models;
using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Mapper
{
    public class GroupMapper:Profile
    {
        public GroupMapper(){
            CreateMap<UpdateGroupDto,Groups>();
            CreateMap<Groups,ReadGroupDto>();
        }
        
    }
}
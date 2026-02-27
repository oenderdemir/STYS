namespace TOD.Platform.Persistence.Rdbms.Dto;

public abstract class BaseRdbmsDto<TKey> where TKey : struct
{
    public TKey? Id { get; set; }
}

public abstract class BaseRdbmsDto : BaseRdbmsDto<Guid>
{
}
